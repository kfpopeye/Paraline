using System;
using log4net;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace paraline
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class explodeDC_command : IExternalCommand
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(explodeDC_command));

        public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
#if !DEBUG
            if (!paraline.UserIsEntitled(commandData))
                return Result.Failed;
#endif

            Autodesk.Revit.DB.Document actdoc = commandData.Application.ActiveUIDocument.Document;
            Transaction theTransaction = null;
            List<DetailElement> transformedElements = new List<DetailElement>();
            Autodesk.Revit.DB.Document theProjectDoc = commandData.Application.ActiveUIDocument.Document;
            ElementSet filteredSelection = new ElementSet();

            //Note: there is no such thing as in-place detail component families
            foreach (ElementId eid in commandData.Application.ActiveUIDocument.Selection.GetElementIds())
            {
                Element e = theProjectDoc.GetElement(eid);
                if (e.GetType() == typeof(FamilyInstance) && e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DetailComponents)
                    filteredSelection.Insert(e);
                else if (e.GetType() == typeof(DetailLine) && e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DetailComponents)
                {
                    DetailLine dl = e as DetailLine;
                    if (dl.CurveElementType == CurveElementType.RepeatingDetail)
                        filteredSelection.Insert(e);
                }
            }

            if (filteredSelection.Size < 1)
            {
                TaskDialog.Show(Constants.EDC_MACRO_NAME, "There were no detail components in the selection to convert.");
                return Result.Cancelled;
            }

            try
            {
                int counter = 0;
                foreach (Element e in filteredSelection)
                {
                    List<DetailElement> lid = null;
                    if (e.GetType() == typeof(DetailLine))
                    {
                        lid = importRepeatingDetailElements(theProjectDoc, e, actdoc.ActiveView.DetailLevel);
                    }
                    else
                    {
                        lid = importDraftingElements(theProjectDoc, e, actdoc.ActiveView.DetailLevel);
                    }
                    if (lid != null)
                    {
                        transformedElements.AddRange(lid);
                        ++counter;
                    }
                }

                if (transformedElements.Count < 1)
                {
                    TaskDialog.Show(Constants.EDC_MACRO_NAME, "Could not convert any of the detail components.");
                    return Result.Cancelled;
                }

                //add items to active view
                theTransaction = new Transaction(theProjectDoc, Constants.EDC_MACRO_NAME);
                theTransaction.Start();
                foreach (DetailElement de in transformedElements)
                {
                    FilledRegion fr = null;
                    DetailCurve dc = null;
                    //TODO: match family detail lines to project lines. API does not allow yet graphicstyle has no link to line pattern
                    if (de.isFilledRegion)
                        fr = FilledRegion.Create(theProjectDoc, getFilledRegionType(de, theProjectDoc), theProjectDoc.ActiveView.Id, de.theRegion);
                    else
                        dc = theProjectDoc.Create.NewDetailCurve(theProjectDoc.ActiveView, de.theCurve);
                }

                //delete originals?
                TaskDialog td = new TaskDialog(Constants.EDC_MACRO_NAME);
                td.MainInstruction = string.Format("Successfully converted {0} of {1} detail components.", counter, filteredSelection.Size);
                td.MainContent = "Delete original detail components?";
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Yes, delete components.");
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "No, leave them be.");
                TaskDialogResult tdr = td.Show();
                if (tdr == TaskDialogResult.CommandLink1)
                {
                    foreach (Element e in filteredSelection)
                        actdoc.Delete(e.Id);
                }
                theTransaction.Commit();
                return Result.Succeeded;
            }
            catch (Exception err)
            {
                Autodesk.Revit.UI.TaskDialog td = new TaskDialog("Unexpected Error");
                td.MainInstruction = Constants.EDC_MACRO_NAME + " command caused an unknown error.";
                td.MainContent = "Something unexpected has happened and the command cannot complete. More information can be found below.";
                td.ExpandedContent = err.ToString();
                //td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Send bug report.");
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                TaskDialogResult tdr = td.Show();

                //if (tdr == TaskDialogResult.CommandLink1)
                //    pkhCommon.Email.SendErrorMessage(commandData.Application.Application.VersionName, commandData.Application.Application.VersionBuild, err, this.GetType().Assembly.GetName());

                _log.Error(err);
                if (theTransaction != null && theTransaction.HasStarted())
                    theTransaction.RollBack();
                return Result.Failed;
            }
        }

        //for repeating details
        private List<DetailElement> importRepeatingDetailElements(Document theProjectDoc, Element e, ViewDetailLevel detailLevel)
        {
            double dc_rotation = 0;
            XYZ dc_translate = new XYZ(0, 0, 0);

            DetailLine dl = e as DetailLine;
            ElementType repDetail = theProjectDoc.GetElement(dl.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsElementId()) as ElementType;
            FamilySymbol detailComp = theProjectDoc.GetElement(repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_ELEMENT).AsElementId()) as FamilySymbol;
            int detRotat = repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_ROTATION).AsInteger(); //0-none, 1-90 deg cw, 2-180 deg, 3-90 deg ccw (affected by inside param)
            bool inside = repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_INSIDE).AsInteger() == 1;
            Curve locat = dl.GeometryCurve;
            BoundingBoxXYZ box = detailComp.get_BoundingBox(null);

            List<DetailElement> baseElements = importDraftingElements(theProjectDoc, detailComp, detailLevel);

            // create rotation for orientation to layout line
            XYZ aa = locat.GetEndPoint(0);
            XYZ cc = new XYZ(aa.X, aa.Y, aa.Z + 10);
            Line rotationAxis = Line.CreateBound(aa, cc);
            double lineRotat = GetAngleOfLineBetweenEndPoints(locat);
            dc_rotation = lineRotat - (Math.PI / 2); //less 90 degrees for detail components which are perpendicular to layout line
            if (detRotat != 0)
                dc_rotation += Math.PI / 2 * -detRotat; //increment rotation based on rdc setting

            //create move vector based on detail component size
            if (detRotat != 0 && inside)
            {
                double len = 0;
                double x1 = 0;
                double y1 = 0;
                switch (detRotat) //0-none, 1-90 deg cw, 2-180 deg, 3-90 deg ccw (affected by inside param)
                {
                    case 1:
                        len = box.Max.X - box.Min.X;
                        x1 = len * Math.Cos(lineRotat);
                        y1 = len * Math.Sin(lineRotat);
                        dc_translate = new XYZ(x1, y1, 0);
                        break;
                    case 2:
                        len = box.Max.Y - box.Min.Y;
                        x1 = len * Math.Cos(lineRotat);
                        y1 = len * Math.Sin(lineRotat);
                        dc_translate = new XYZ(x1, y1, 0);
                        break;
                    default:
                        dc_translate = new XYZ(0, 0, 0);
                        break;
                }
            }

            //check for mirrored rdc
            Transform t = Transform.CreateRotationAtPoint(XYZ.BasisZ, (Math.PI / 2) - lineRotat, locat.GetEndPoint(0));
            Options o = new Options();
            o.IncludeNonVisibleObjects = true;
            GeometryElement ge = dl.get_Geometry(o);
            Line l = ge.ElementAt<GeometryObject>(2) as Line;
            l = l.CreateTransformed(t) as Line;
            bool Y_mirrored = l.Direction.X < 0;
            l = ge.ElementAt<GeometryObject>(0) as Line;
            l = l.CreateTransformed(t) as Line;
            bool X_mirrored = l.Direction.Y < 0;

            // create a vector for spacing
            double dl_length = dl.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
            int layout = repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_LAYOUT).AsInteger();
            double rep_det_spacing = 0;
            int rep_det_number = dl.get_Parameter(BuiltInParameter.REPEATING_DETAIL_NUMBER).AsInteger();
            XYZ spacing_vector = null;
            switch (layout)  //1-fixed dist, 2-fixed num, 3-max spacing, 4-fill available
            {
                case 1:
                    rep_det_spacing = repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_SPACING).AsDouble();
                    break;
                case 2:
                    if (inside)
                    {
                        if (detRotat == 1 || detRotat == 3)
                            rep_det_spacing = (dl_length - (box.Max.X - box.Min.X)) / (rep_det_number - 1);
                        else
                            rep_det_spacing = (dl_length - (box.Max.Y - box.Min.Y)) / (rep_det_number - 1);
                    }
                    else
                        rep_det_spacing = dl_length / (rep_det_number - 1);
                    break;
                case 3:
                    int num = 0;
                    rep_det_spacing = repDetail.get_Parameter(BuiltInParameter.REPEATING_DETAIL_SPACING).AsDouble();
                    if (inside)
                    {
                        if (detRotat == 1 || detRotat == 3)
                            dl_length -= (box.Max.X - box.Min.X);
                        else
                            dl_length -= (box.Max.Y - box.Min.Y);
                        num = (int)(dl_length / rep_det_spacing) + 1;
                        rep_det_spacing = dl_length / (double)num;
                    }
                    else
                    {
                        num = (int)(dl_length / rep_det_spacing) + 1;
                        rep_det_spacing = dl_length / (double)num;
                    }
                    break;
                case 4:
                    rep_det_spacing = box.Max.Y - box.Min.Y;
                    break;
                default:
                    break;
            }
            double x = rep_det_spacing * Math.Cos(lineRotat);
            double y = rep_det_spacing * Math.Sin(lineRotat);
            spacing_vector = new XYZ(x, y, 0);

            if (X_mirrored)
            {
                Transform tr = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero));
                foreach (DetailElement de in baseElements)
                {
                    de.transformDetailElement(tr);
                }
            }
            if (Y_mirrored)
            {
                Transform tr = Transform.CreateReflection(Plane.CreateByNormalAndOrigin(XYZ.BasisX, XYZ.Zero));
                foreach (DetailElement de in baseElements)
                {
                    de.transformDetailElement(tr);
                }
            }

            Transform t1 = Transform.CreateRotationAtPoint(XYZ.BasisZ, dc_rotation, XYZ.Zero);
            Transform t2 = Transform.CreateTranslation(dc_translate);
            Transform t3 = Transform.CreateTranslation(aa);
            foreach (DetailElement de in baseElements)
            {
                de.transformDetailElement(t1);
                de.transformDetailElement(t2);
                de.transformDetailElement(t3);
            }

            List<DetailElement> transformedElements = new List<DetailElement>();
            for (int c = 0; c < rep_det_number; c++)
            {
                Transform trfm = Transform.CreateTranslation(spacing_vector.Multiply(c));
                foreach (DetailElement de in baseElements)
                {
                    DetailElement d = de.Clone() as DetailElement;
                    d.transformDetailElement(trfm);
                    transformedElements.Add(d);
                }
            }

            return transformedElements;
        }

        private List<DetailElement> importDraftingElements(Document theParentDoc, Element theFamilyElement, ViewDetailLevel parentViewDetailLevel)
        {
            Transaction familyTransaction = null;
            SubTransaction sft = null;
            Document theFamilyDoc = null;
            FamilyInstance fi = null;
            string SymbolName = null;
            string FamilyName = null;
            List<DetailElement> transformedElements = new List<DetailElement>();
            Transform mirrorFace = null;
            Transform TransformToFamily = null;

            if (theFamilyElement is FamilyInstance)
            {
                fi = theFamilyElement as FamilyInstance;
                theFamilyDoc = theParentDoc.EditFamily(fi.Symbol.Family);
                SymbolName = fi.Symbol.Name;
                FamilyName = fi.Symbol.Family.Name;
            }
            else if (theFamilyElement is FamilySymbol)
            {
                FamilySymbol fs = theFamilyElement as FamilySymbol;
                theFamilyDoc = theParentDoc.EditFamily(fs.Family);
                SymbolName = fs.Name;
                FamilyName = fs.Family.Name;
            }
            familyTransaction = new Transaction(theFamilyDoc, Constants.EDC_MACRO_NAME);
            familyTransaction.Start();

            sft = new SubTransaction(theFamilyDoc);
            sft.Start();
            //set the sibling to match what is in the project so type parameters match
            FamilyTypeSetIterator ftsi = theFamilyDoc.FamilyManager.Types.ForwardIterator();
            while (ftsi.MoveNext())
            {
                FamilyType ft = ftsi.Current as FamilyType;
                if (ft.Name == SymbolName)
                {
                    theFamilyDoc.FamilyManager.CurrentType = ft;
                    break;
                }
            }
            if (fi != null)
            {
                //set instance parameters to match project instance
                foreach (Parameter p in fi.Parameters)
                {
                    FamilyParameter fp = theFamilyDoc.FamilyManager.get_Parameter(p.Definition.Name);
                    if (fp != null && !fp.IsReadOnly && !fp.IsDeterminedByFormula && fp.IsInstance)
                    {
                        switch (fp.StorageType)
                        {
                            case StorageType.Integer:
                                theFamilyDoc.FamilyManager.Set(fp, p.AsInteger());
                                break;
                            case StorageType.Double:
                                theFamilyDoc.FamilyManager.Set(fp, p.AsDouble());
                                break;
                            case StorageType.ElementId:
                                theFamilyDoc.FamilyManager.Set(fp, p.AsElementId());
                                break;
                            case StorageType.String:
                                theFamilyDoc.FamilyManager.Set(fp, p.AsString());
                                break;
                            default:
                                theFamilyDoc.FamilyManager.SetValueString(fp, p.AsValueString());
                                break;
                        }
                    }
                }
            }
            if (sft.Commit() == TransactionStatus.Error)
            {
                TaskDialog.Show(Constants.EDC_MACRO_NAME, "Could not explode " + FamilyName + " : " + SymbolName + ". Skipping it.");
                if (sft.HasStarted())
                    sft.RollBack();
                if (familyTransaction.HasStarted())
                    familyTransaction.RollBack();
                theFamilyDoc.Close(false);
                return null;
            }

            transformedElements = getDetailElements(theFamilyDoc, parentViewDetailLevel);

            //create family instance transforms
            if (fi != null)
            {
                if (fi.Mirrored)
                    mirrorFace = Transform.CreateReflection(theFamilyDoc.FamilyCreate.NewReferencePlane(XYZ.Zero, XYZ.BasisX, XYZ.BasisZ, theFamilyDoc.ActiveView).GetPlane());
                TransformToFamily = fi.GetTotalTransform();

                foreach (DetailElement de in transformedElements)
                {
                    if (mirrorFace != null)
                        de.transformDetailElement(mirrorFace);
                    de.transformDetailElement(TransformToFamily);
                }
            }

            familyTransaction.RollBack(); //must close transactions before modifying sub families

            //nested family symbols
            FilteredElementCollector collector2 = new FilteredElementCollector(theFamilyDoc);
            collector2.OfClass(typeof(FamilyInstance));
            FilteredElementIterator itor2 = collector2.GetElementIterator();
            itor2.Reset();
            while (itor2.MoveNext())
            {
                Element e = itor2.Current as Element;
                List<DetailElement> te = importDraftingElements(theFamilyDoc, e, parentViewDetailLevel);
                foreach (DetailElement de in te)
                {
                    if (fi != null)
                    {
                        if (mirrorFace != null)
                            de.transformDetailElement(mirrorFace);
                        de.transformDetailElement(TransformToFamily);
                    }
                    transformedElements.Add(de);
                }
            }

            theFamilyDoc.Close(false);
            if (transformedElements.Count < 1)
                return null;
            else
                return transformedElements;
        }

        /// <summary>
        /// Creates a list of all filled regions and lines in a family document
        /// </summary>
        /// <param name="theFamilyDoc"></param>
        /// <param name="parentViewDetailLevel">The detail level of the hosting view</param>
        /// <returns>Null if none found</returns>
        private List<DetailElement> getDetailElements(Document theFamilyDoc, ViewDetailLevel parentViewDetailLevel)
        {
            List<DetailElement> detailElements = new List<DetailElement>();

            //create lines, arcs and ellipses
            FilteredElementCollector collector = new FilteredElementCollector(theFamilyDoc);
            collector.OfCategory(BuiltInCategory.OST_Lines);
            FilteredElementIterator itor = collector.GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                DetailCurve sc = itor.Current as DetailCurve;
                if (IsVisibile(sc, parentViewDetailLevel))
                {
                    DetailElement de = new DetailElement(sc.GeometryCurve);
                    detailElements.Add(de);
                }
            }

            //create transformed filled regions
            collector = new FilteredElementCollector(theFamilyDoc);
            collector.OfClass(typeof(FilledRegion));
            itor = collector.GetElementIterator();
            itor.Reset();
            while (itor.MoveNext())
            {
                FilledRegion e = itor.Current as FilledRegion;
                //TODO: check if region is visible in current detail level. API has no way yet
                if (e.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM).AsInteger() == 1)
                {
                    DetailElement de = null;
                    if (!e.IsMasking)
                        de = new DetailElement(e.GetBoundaries(), theFamilyDoc.GetElement(e.GetTypeId()) as FilledRegionType);
                    else
                        de = new DetailElement(e.GetBoundaries(), null);
                    detailElements.Add(de);
                }
            }

            if (detailElements.Count < 1)
                return null;
            else
                return detailElements;
        }

        /// <summary>
        /// Determines the angle of a straight line drawn between point one and two. 
        /// The number returned, which is a double in radians, tells us how much we have to 
        /// rotate a horizontal line clockwise for it to match the line between the two points.
        /// </summary>
        public static double GetAngleOfLineBetweenEndPoints(Curve c)
        {
            XYZ p1 = c.GetEndPoint(0);
            XYZ p2 = c.GetEndPoint(1);
            double xDiff = p2.X - p1.X;
            double yDiff = p2.Y - p1.Y;
            return Math.Atan2(yDiff, xDiff);
        }

        private ElementId getFilledRegionType(DetailElement de, Document doc)
        {
            //used for creating new types later on.
            ElementId firstRegionTypeFound = null;

            //check existing fill region types in project for a match
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(FilledRegionType));
            foreach (FilledRegionType f in collector)
            {
                if (firstRegionTypeFound == null)
                    firstRegionTypeFound = f.Id;
                if (de.areMatchingFillTypes(f))
                    return f.Id;
            }

            if (firstRegionTypeFound == null)
                throw new ArgumentOutOfRangeException("FilledRegionType", "Could not find a filled region type in the document.");

            //create a new fillregiontype in project because it only exists in the detail family
            if (de.regionForeGroundFillPattName == Constants.SolidPattName) //is it the solid fill pattern
            {
                foreach (FilledRegionType frt in collector)
                {
                    if (frt.Name == Constants.SolidPattName)
                        return frt.Id;
                }
                SubTransaction st = new SubTransaction(doc);
                st.Start();

                FillPatternElement fpid = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, Constants.SolidPattName);
                if (fpid == null)
                {
                    FilteredElementCollector elements = new FilteredElementCollector(doc);
                    elements.WherePasses(new ElementClassFilter(typeof(FillPatternElement)));
                    foreach (FillPatternElement fpe in elements)
                    {
                        if (fpe.GetFillPattern().IsSolidFill)
                        {
                            fpid = fpe;
                            break;
                        }
                    }
                }

                FilledRegionType newFRtype = doc.GetElement(firstRegionTypeFound) as FilledRegionType;
                newFRtype = newFRtype.Duplicate(Constants.SolidPattName) as FilledRegionType;
                newFRtype.ForegroundPatternId = fpid.Id;
                newFRtype.ForegroundPatternColor = new Color(255, 255, 255);
                newFRtype.IsMasking = true;

                st.Commit();
                return newFRtype.Id;
            }
            else
            {
                foreach (FilledRegionType frt in collector)
                {
                    if (frt.Name == de.getNormalizedName())
                        return frt.Id;
                }
                SubTransaction st = new SubTransaction(doc);
                st.Start();
                FilledRegionType newFRtype = doc.GetElement(firstRegionTypeFound) as FilledRegionType;
                newFRtype = newFRtype.Duplicate(de.getNormalizedName()) as FilledRegionType;
                if (de.regionForeGroundFillPattName != null)
                    newFRtype.ForegroundPatternId = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, de.regionForeGroundFillPattName).Id;
                if (de.regionBackGroundFillPattName != null)
                    newFRtype.BackgroundPatternId = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, de.regionBackGroundFillPattName).Id;
                de.matchFillType(newFRtype);
                st.Commit();
                return newFRtype.Id;
            }
        }

        /// <summary>
        /// Checks to see if the detailcurve is visible either by parameter or visibility setting (IE. course, medium, fine).
        /// </summary>
        public bool IsVisibile(Element theElement, ViewDetailLevel theDetailLevel)
        {
            if (theElement.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM).AsInteger() != 1)
                return false;

            Parameter visParam = theElement.get_Parameter(BuiltInParameter.GEOM_VISIBILITY_PARAM);
            int vis = visParam.AsInteger();
            int mask = 0;
            switch (theDetailLevel)
            {
                case ViewDetailLevel.Coarse:
                    //vis = vis & ~(1 << 13); // Coarse (~ turns of the bit)
                    mask = (1 << 13);
                    break;
                case ViewDetailLevel.Medium:
                    //vis = vis & ~(1 << 14); // Medium
                    mask = (1 << 14);
                    break;
                case ViewDetailLevel.Fine:
                    //vis = vis & ~(1 << 15); // Fine
                    mask = (1 << 15);
                    break;
                default:
                    throw new NotSupportedException();
            }

            if ((vis & mask) == mask)
                return true;
            else
                return false;
        }
    }

    public class explodeDC_AvailableCheck : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication appdata, CategorySet selectCatagories)
        {
            if (appdata.Application.Documents.Size == 0)
                return false;
            if (appdata.ActiveUIDocument.Selection.GetElementIds().Count == 0)
                return false;

            return true;
        }
    }
}