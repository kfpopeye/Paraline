
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace paraline
{
	/// <summary>
	/// A transformed element (lines or filled region).
	/// </summary>
	public class DetailElement : ICloneable
	{
		public List<CurveLoop> theRegion = null;
		public Curve theCurve = null;
        private Color foreGroundColour = null;
        private Color backGroundColour = null;
        private int fillWeight = 0;
        private bool isFillMasking = false;
        private string foreGroundPattName = null;
        private string backGroundPattName = null;
        private string regionTypeName = null;

        //public string regionFillPattName
        //{
        //    get
        //    {
        //        return fillPattName;
        //    }
        //} 2019

        /// <summary>
        /// Pattern used for solid patterns
        /// </summary>
        public string regionForeGroundFillPattName
        {
            get
            {
                return foreGroundPattName;
            }
        }

        public string regionBackGroundFillPattName
        {
            get
            {
                return backGroundPattName;
            }
        }

        public bool isFilledRegion
		{
			get
			{
				return(theRegion != null);
			}
		}
		
		public DetailElement(IList<CurveLoop> cl, FilledRegionType frt)
		{
			CurveLoop[] cla = new CurveLoop[cl.Count];
			cl.CopyTo(cla, 0);
			theRegion = new List<CurveLoop>();
			this.theRegion.AddRange(cla);

            if (frt != null)
            {
                regionTypeName = frt.Name;
                this.foreGroundColour = new Color(frt.ForegroundPatternColor.Red, frt.ForegroundPatternColor.Green, frt.ForegroundPatternColor.Blue);
                this.backGroundColour = new Color(frt.BackgroundPatternColor.Red, frt.BackgroundPatternColor.Green, frt.BackgroundPatternColor.Blue);
                this.fillWeight = frt.LineWeight;
                this.isFillMasking = frt.IsMasking;
                Element el = frt.Document.GetElement(frt.ForegroundPatternId);    //DWG imports don't have a fill pattern sometimes
                if (el != null)
                    this.foreGroundPattName = el.Name;
                el = frt.Document.GetElement(frt.BackgroundPatternId);
                if (el != null)
                    this.backGroundPattName = el.Name;
            }
            else  //    for masking regions
            {
                this.foreGroundPattName = Constants.SolidPattName;
                this.foreGroundColour = new Color(255, 255, 255);
                this.fillWeight = 1;
            }
		}
		
		public DetailElement(Curve c)
		{
			theCurve = c.Clone();
        }

        private DetailElement(DetailElement rhs)
        {
            IList<CurveLoop> cl = rhs.theRegion;
            CurveLoop[] cla = new CurveLoop[cl.Count];
            cl.CopyTo(cla, 0);
            theRegion = new List<CurveLoop>();
            this.theRegion.AddRange(cla);

            if (rhs.regionTypeName != null)
            {
                regionTypeName = rhs.regionTypeName;
                this.foreGroundColour = rhs.foreGroundColour;
                this.backGroundColour = rhs.backGroundColour;
                this.fillWeight = rhs.fillWeight;
                this.isFillMasking = rhs.isFillMasking;
                this.foreGroundPattName = rhs.foreGroundPattName;
                this.backGroundPattName = rhs.backGroundPattName;
            }
            else  //    for masking regions
            {
                this.foreGroundPattName = Constants.SolidPattName;
                this.foreGroundColour = new Color(255, 255, 255);
                this.fillWeight = 1;
            }
        }

        /// <summary>
        /// Returns pkh- plus the filled region type name
        /// </summary>
        /// <returns></returns>
        public string getNormalizedName()
        {
            if (regionTypeName == null)
                throw new NullReferenceException("RegionTypeName was null.");
            return "pkh-" + regionTypeName;
        }

        public void transformDetailElement(Transform theTransform)
		{
			if(isFilledRegion)
			{
				List<CurveLoop> cl_list = new List<CurveLoop>();
				List<Curve> c_list = new List<Curve>();
				
				foreach(CurveLoop cl in theRegion)
				{
					CurveLoopIterator cli = cl.GetCurveLoopIterator();
					cli.Reset();
					while(cli.MoveNext())
					{
						Curve c = cli.Current.CreateTransformed(theTransform);
						c_list.Add(c);
					}
					cl_list.Add(CurveLoop.Create(c_list));
					c_list.Clear();
				}
				
				theRegion = cl_list;
			}
			else
			{
				theCurve = theCurve.CreateTransformed(theTransform);
			}
		}
		
        /// <summary>
        /// Compares foreground and background pattern names and colours, and fill weight and opacity
        /// </summary>
        /// <param name="rhs"></param>
        /// <returns></returns>
		public bool areMatchingFillTypes(FilledRegionType rhs)
		{            
            Element el = rhs.Document.GetElement(rhs.ForegroundPatternId);

            if((el == null && this.foreGroundPattName != null) ||
                (el != null && this.foreGroundPattName == null))
                return false;

            if (el != null)
                if (this.foreGroundPattName != el.Name)
                    return false;

            el = rhs.Document.GetElement(rhs.BackgroundPatternId);

            if ((el == null && this.backGroundPattName != null) ||
                (el != null && this.backGroundPattName == null))
                return false;

            if (el != null)
                if (this.backGroundPattName != el.Name)
                    return false;

            if ((this.foreGroundColour.Blue != rhs.ForegroundPatternColor.Blue) ||
                (this.foreGroundColour.Green != rhs.ForegroundPatternColor.Green) ||
                (this.foreGroundColour.Red != rhs.ForegroundPatternColor.Red))
                return false;

            if ((this.backGroundColour.Blue != rhs.BackgroundPatternColor.Blue) ||
                (this.backGroundColour.Green != rhs.BackgroundPatternColor.Green) ||
                (this.backGroundColour.Red != rhs.BackgroundPatternColor.Red))
                return false;

            if (this.fillWeight != rhs.LineWeight)
                return false;

            if (this.isFillMasking != rhs.IsMasking)
                return false;

            return true;
		}

        /// <summary>
        /// Sets the opacity, lineweight, foreground colour and background colour of the filledregiontype
        /// </summary>
        /// <param name="rhs"></param>
        public void matchFillType(FilledRegionType rhs)
        {
            rhs.IsMasking = this.isFillMasking;
            if (this.foreGroundPattName != Constants.SolidPattName && FilledRegionType.IsValidLineWeight(this.fillWeight) && rhs.LineWeight != this.fillWeight)
                rhs.LineWeight = this.fillWeight;
            if ((this.foreGroundColour.Blue != rhs.ForegroundPatternColor.Blue) || 
                (this.foreGroundColour.Green != rhs.ForegroundPatternColor.Green) || 
                (this.foreGroundColour.Red != rhs.ForegroundPatternColor.Red)
                && !rhs.ForegroundPatternColor.IsReadOnly)
                rhs.ForegroundPatternColor = new Color(this.foreGroundColour.Red, this.foreGroundColour.Green, this.foreGroundColour.Blue);
            if ((this.backGroundColour.Blue != rhs.BackgroundPatternColor.Blue) ||
                (this.backGroundColour.Green != rhs.BackgroundPatternColor.Green) ||
                (this.backGroundColour.Red != rhs.BackgroundPatternColor.Red)
                && !rhs.BackgroundPatternColor.IsReadOnly)
                rhs.BackgroundPatternColor = new Color(this.backGroundColour.Red, this.backGroundColour.Green, this.backGroundColour.Blue);
        }

        public object Clone()
        {
            if (theCurve != null)
                return new DetailElement(theCurve);
            else
                return new DetailElement(this);
        }
    }
}
