using System;
using System.Collections.Generic;
using log4net;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;

namespace paraline
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public partial class ortho2iso_command : IExternalCommand
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(ortho2iso_command));
        private XYZ origin = XYZ.Zero;
        private ElementSet sel = null;
        private Autodesk.Revit.DB.Document actdoc = null;
        private bool outlineRegions = false;
        private bool deleteOriginals = false;

        public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
#if !DEBUG
            if (!paraline.UserIsEntitled(commandData))
                return Result.Failed;
#endif

            actdoc = commandData.Application.ActiveUIDocument.Document;
            sel = new ElementSet();

            //TODO: support masking regions. Currently API has no way to do this.

            // Filter the non-supported types in the selection set
            foreach (ElementId eid in commandData.Application.ActiveUIDocument.Selection.GetElementIds())
            {
                bool maskingWarningNotShown = true;
                Element e = actdoc.GetElement(eid);
                if (e.GetType() == typeof(Autodesk.Revit.DB.DetailLine) ||
                    e.GetType() == typeof(Autodesk.Revit.DB.DetailArc) ||
                    e.GetType() == typeof(Autodesk.Revit.DB.DetailEllipse) ||
                    e.GetType() == typeof(Autodesk.Revit.DB.DetailNurbSpline))
                    sel.Insert(e);
                if (e.GetType() == typeof(Autodesk.Revit.DB.FilledRegion))
                {
                    Autodesk.Revit.DB.FilledRegion fr = e as Autodesk.Revit.DB.FilledRegion;
                    if (!fr.IsMasking)
                        sel.Insert(e);
                    else
                        if (maskingWarningNotShown)
                        {
                            TaskDialog.Show("Masking Region detected", "Masking regions are currently not supported and will be ignored.");
                            maskingWarningNotShown = false;
                        }
                }
                if (e.GetType() == typeof(Autodesk.Revit.DB.FamilyInstance) && e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DetailComponents)
                    TaskDialog.Show("Family Detail Component", "A detail component family was detected. Use the " + Constants.EDC_MACRO_NAME + " command to explode it first.");
            }

            Transaction transaction = new Transaction(actdoc);
            try
            {
                if (sel.IsEmpty)
                {
                    TaskDialog.Show(Constants.O2I_MACRO_NAME, "No supported elements were selected.");
                    return Result.Cancelled;
                }
                else
                {
                    transaction.Start(Constants.O2I_MACRO_NAME);
                    IsoMaker_Main_Window imw = new IsoMaker_Main_Window(sel, actdoc);
                    System.Windows.Interop.WindowInteropHelper x = new System.Windows.Interop.WindowInteropHelper(imw);
                    x.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                    if ((bool)imw.ShowDialog())
                    {
                        setOrigin();
                        outlineRegions = imw.outline;
                        deleteOriginals = imw.delete;
                        switch(imw.UsersChoice)
                        {
                            case IsoMaker_Main_Window.UsersChoices.Top:
                                ConvertToTopView();
                                break;
                            case IsoMaker_Main_Window.UsersChoices.Right:
                                ConvertToRightView();
                                break;
                            case IsoMaker_Main_Window.UsersChoices.Left:
                                ConvertToLeftView();
                                break;
                        }
                        transaction.Commit();
                        return Result.Succeeded;
                    }
                    else
                    {
                        transaction.RollBack();
                        return Result.Cancelled;
                    }
                }
            }
            catch (Exception err)
            {
                _log.Error(err);
                Autodesk.Revit.UI.TaskDialog td = new TaskDialog("Unexpected Error");
                td.MainInstruction = Constants.O2I_MACRO_NAME + " command caused an unknown error.";
                td.MainContent = "Something unexpected has happened and the command cannot complete. More information can be found below.";
                td.ExpandedContent = err.ToString();
                //td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Send bug report.");
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                TaskDialogResult tdr = td.Show();

                //if (tdr == TaskDialogResult.CommandLink1)
                //    pkhCommon.Email.SendErrorMessage(commandData.Application.Application.VersionName, commandData.Application.Application.VersionBuild, err, this.GetType().Assembly.GetName());

                if (transaction != null && transaction.HasStarted())
                    transaction.RollBack();
                return Result.Failed;
            }
        }

        void ConvertToTopView()
        {
            bool hasLineTooShort = false;
            foreach (Element el in sel)
            {
                try
                {
                    if (el.GetType() == typeof(Autodesk.Revit.DB.FilledRegion))
                    {
                        List<CurveLoop> li = new List<CurveLoop>();
                        FilledRegion fr = el as FilledRegion;
                        IList<CurveLoop> cl_list = fr.GetBoundaries();
                        foreach (CurveLoop cl in cl_list)
                        {
                            List<Curve> NewBoundaries = new List<Curve>();
                            System.Collections.IEnumerator c_en = cl.GetEnumerator();
                            while (c_en.MoveNext())
                            {
                                Curve crv = c_en.Current as Curve;
                                if (crv.GetType() == typeof(Autodesk.Revit.DB.Arc))
                                {
                                    Arc a = crv as Arc;
                                    Ellipse ell = null;
                                    if (a.IsBound)
                                        ell = isoArc_Top(a);
                                    else
                                        ell = isoCircle_Top(a);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ell);
                                    NewBoundaries.Add(ell as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Line))
                                {
                                    Line l = crv as Line;
                                    l = isoLine_Top(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                                {
                                    Ellipse l = crv as Ellipse;
                                    if (l.IsBound)
                                        l = isoEllipseArc_Top(l);
                                    else
                                        l = isoEllipse_Top(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                                {
                                    NurbSpline ns = crv as NurbSpline;
                                    ns = isoSpline_Top(ns);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ns);
                                    NewBoundaries.Add(ns as Curve);
                                }
                            }
                            li.Add(CurveLoop.Create(OrderBoundaries(NewBoundaries)));
                        }
                        FilledRegion.Create(actdoc, fr.GetTypeId(), actdoc.ActiveView.Id, li);
                    }
                    else
                    {
                        Curve transformedCurve = null;
                        LocationCurve lc = el.Location as LocationCurve;
                        if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Arc))
                        {
                            Arc a = lc.Curve as Arc;
                            if (a.IsBound)
                                transformedCurve = isoArc_Top(a) as Curve;
                            else
                                transformedCurve = isoCircle_Top(a) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Line))
                        {
                            Line l = lc.Curve as Line;
                            transformedCurve = isoLine_Top(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                        {
                            Ellipse l = lc.Curve as Ellipse;
                            if (l.IsBound)
                                transformedCurve = isoEllipseArc_Top(l) as Curve;
                            else
                                transformedCurve = isoEllipse_Top(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                        {
                            NurbSpline l = lc.Curve as NurbSpline;
                            transformedCurve = isoSpline_Top(l) as Curve;
                        }

                        CurveElement ce = el as CurveElement;
                        DetailCurve dc = null;
                        if (actdoc.IsFamilyDocument)
                            dc = actdoc.FamilyCreate.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        else
                            dc = actdoc.Create.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        dc.LineStyle = ce.LineStyle;
                    }

                    if (deleteOriginals)
                        actdoc.Delete(el.Id);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentsInconsistentException)
                {
                    //this error is generated if the curve length is too small for Revit's tolerance (as identified by Application.ShortCurveTolerance).
                    if (!hasLineTooShort)
                    {
                        TaskDialog.Show(Constants.O2I_MACRO_NAME, "Some lines were too short for Revit after being transformed to isometric. These lines will be ignored. This may also affect filled regions.");
                        hasLineTooShort = true;
                    }
                }
            }
        }

        void ConvertToLeftView()
        {
            bool hasLineTooShort = false;
            foreach (Element el in sel)
            {
                try
                {
                    if (el.GetType() == typeof(Autodesk.Revit.DB.FilledRegion))
                    {
                        List<CurveLoop> li = new List<CurveLoop>();
                        FilledRegion fr = el as FilledRegion;
                        IList<CurveLoop> cl_list = fr.GetBoundaries();
                        foreach (CurveLoop cl in cl_list)
                        {
                            List<Curve> NewBoundaries = new List<Curve>();
                            System.Collections.IEnumerator c_en = cl.GetEnumerator();
                            while (c_en.MoveNext())
                            {
                                Curve crv = c_en.Current as Curve;
                                if (crv.GetType() == typeof(Autodesk.Revit.DB.Arc))
                                {
                                    Arc a = crv as Arc;
                                    Ellipse ell = null;
                                    if (a.IsBound)
                                        ell = isoArc_Left(a);
                                    else
                                        ell = isoCircle_Left(a);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ell);
                                    NewBoundaries.Add(ell as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Line))
                                {
                                    Line l = crv as Line;
                                    l = isoLine_Left(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                                {
                                    Ellipse l = crv as Ellipse;
                                    if (l.IsBound)
                                        l = isoEllipseArc_Left(l);
                                    else
                                        l = isoEllipse_Left(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                                {
                                    NurbSpline ns = crv as NurbSpline;
                                    ns = isoSpline_Side(ns, true);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ns);
                                    NewBoundaries.Add(ns as Curve);
                                }
                            }
                            li.Add(CurveLoop.Create(OrderBoundaries(NewBoundaries)));
                        }
                        FilledRegion.Create(actdoc, fr.GetTypeId(), actdoc.ActiveView.Id, li);
                    }
                    else
                    {
                        LocationCurve lc = el.Location as LocationCurve;
                        Curve transformedCurve = null;
                        if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Arc))
                        {
                            Arc a = lc.Curve as Arc;
                            if (a.IsBound)
                                transformedCurve = isoArc_Left(a) as Curve;
                            else
                                transformedCurve = isoCircle_Left(a) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Line))
                        {
                            Line l = lc.Curve as Line;
                            transformedCurve = isoLine_Left(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                        {
                            Ellipse l = lc.Curve as Ellipse;
                            if (l.IsBound)
                                transformedCurve = isoEllipseArc_Left(l) as Curve;
                            else
                                transformedCurve = isoEllipse_Left(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                        {
                            NurbSpline l = lc.Curve as NurbSpline;
                            transformedCurve = isoSpline_Side(l, true) as Curve;
                        }
                        CurveElement ce = el as CurveElement;
                        DetailCurve dc = null;
                        if (actdoc.IsFamilyDocument)
                            dc = actdoc.FamilyCreate.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        else
                            dc = actdoc.Create.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        dc.LineStyle = ce.LineStyle;
                    }
                    if (deleteOriginals)
                        actdoc.Delete(el.Id);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentsInconsistentException)
                {
                    //this error is generated if the curve length is too small for Revit's tolerance (as identified by Application.ShortCurveTolerance).
                    if (!hasLineTooShort)
                    {
                        TaskDialog.Show(Constants.O2I_MACRO_NAME, "Some lines were too short for Revit after being transformed to isometric. These lines will be ignored. This may also affect filled regions.");
                        hasLineTooShort = true;
                    }
                }
            }
        }

        void ConvertToRightView()
        {
            bool hasLineTooShort = false;
            foreach (Element el in sel)
            {
                try
                {
                    if (el.GetType() == typeof(Autodesk.Revit.DB.FilledRegion))
                    {
                        List<CurveLoop> li = new List<CurveLoop>();
                        FilledRegion fr = el as FilledRegion;
                        IList<CurveLoop> cl_list = fr.GetBoundaries();
                        foreach (CurveLoop cl in cl_list)
                        {
                            List<Curve> NewBoundaries = new List<Curve>();
                            System.Collections.IEnumerator c_en = cl.GetEnumerator();
                            while (c_en.MoveNext())
                            {
                                Curve crv = c_en.Current as Curve;
                                if (crv.GetType() == typeof(Autodesk.Revit.DB.Arc))
                                {
                                    Arc a = crv as Arc;
                                    Ellipse ell = null;
                                    if (a.IsBound)
                                        ell = isoArc_Right(a);
                                    else
                                        ell = isoCircle_Right(a);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ell);
                                    NewBoundaries.Add(ell as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Line))
                                {
                                    Line l = crv as Line;
                                    l = isoLine_Right(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                                {
                                    Ellipse l = crv as Ellipse;
                                    if (l.IsBound)
                                        l = isoEllipseArc_Right(l);
                                    else
                                        l = isoEllipse_Right(l);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, l);
                                    NewBoundaries.Add(l as Curve);
                                }
                                else if (crv.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                                {
                                    NurbSpline ns = crv as NurbSpline;
                                    ns = isoSpline_Side(ns, false);
                                    if (outlineRegions)
                                        actdoc.Create.NewDetailCurve(actdoc.ActiveView, ns);
                                    NewBoundaries.Add(ns as Curve);
                                }
                            }
                            li.Add(CurveLoop.Create(OrderBoundaries(NewBoundaries)));
                        }
                        FilledRegion.Create(actdoc, fr.GetTypeId(), actdoc.ActiveView.Id, li);
                    }
                    else
                    {
                        Curve transformedCurve = null;
                        LocationCurve lc = el.Location as LocationCurve;
                        if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Arc))
                        {
                            Arc a = lc.Curve as Arc;
                            if (a.IsBound)
                                transformedCurve = isoArc_Right(a) as Curve;
                            else
                                transformedCurve = isoCircle_Right(a) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Line))
                        {
                            Line l = lc.Curve as Line;
                            transformedCurve = isoLine_Right(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
                        {
                            Ellipse l = lc.Curve as Ellipse;
                            if (l.IsBound)
                                transformedCurve = isoEllipseArc_Right(l) as Curve;
                            else
                                transformedCurve = isoEllipse_Right(l) as Curve;
                        }
                        else if (lc.Curve.GetType() == typeof(Autodesk.Revit.DB.NurbSpline))
                        {
                            NurbSpline l = lc.Curve as NurbSpline;
                            transformedCurve = isoSpline_Side(l, false) as Curve;
                        }
                        CurveElement ce = el as CurveElement;
                        DetailCurve dc = null;
                        if (actdoc.IsFamilyDocument)
                            dc = actdoc.FamilyCreate.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        else
                            dc = actdoc.Create.NewDetailCurve(actdoc.ActiveView, transformedCurve);
                        dc.LineStyle = ce.LineStyle;
                    }
                    if (deleteOriginals)
                        actdoc.Delete(el.Id);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentsInconsistentException)
                {
                    //this error is generated if the curve length is too small for Revit's tolerance (as identified by Application.ShortCurveTolerance).
                    if (!hasLineTooShort)
                    {
                        TaskDialog.Show(Constants.O2I_MACRO_NAME, "Some lines were too short for Revit after being transformed to isometric. These lines will be ignored. This may also affect filled regions.");
                        hasLineTooShort = true;
                    }
                }
            }
        }
    }

    public class ortho2iso_AvailableCheck : IExternalCommandAvailability
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