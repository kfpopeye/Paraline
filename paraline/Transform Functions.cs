using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace paraline
{
	public partial class ortho2iso_command : IExternalCommand
	{
		#region Elliptical Arc Functions
		private Ellipse isoEllipseArc_Right(Ellipse theEllipse)
		{
			return transformEllipseArc(theEllipse, false);
		}
		
		private Ellipse isoEllipseArc_Left(Ellipse theEllipse)
		{
			return transformEllipseArc(theEllipse, true);
		}
		
		private Ellipse isoEllipseArc_Top(Ellipse theEllipse)
		{
			if(!theEllipse.IsBound)
				throw new ArgumentException("isoEllipseArc_Top() received a closed arc.");
			
			double sParm, eParm;
			Ellipse isoEllipse = null;
			XYZ sPoint = theEllipse.GetEndPoint(0);
			XYZ ePoint = theEllipse.GetEndPoint(1);
			XYZ mPoint = theEllipse.Evaluate(0.5, true);
			Ellipse e = Ellipse.CreateCurve(theEllipse.Center, theEllipse.RadiusX, theEllipse.RadiusY, theEllipse.XDirection, theEllipse.YDirection, 0, Math.PI * 2) as Ellipse;
			Line parmLine = Line.CreateBound(sPoint, ePoint);
			
			isoEllipse = isoEllipse_Top(e);
			parmLine = isoLine_Top(parmLine);
			mPoint = SSR_point_Top(mPoint);
			
			IntersectionResult ir = isoEllipse.Project(parmLine.GetEndPoint(0));
			sParm = ir.Parameter;
			ir = isoEllipse.Project(parmLine.GetEndPoint(1));
			eParm = ir.Parameter;
			
			if(sParm > eParm)
			{
				double temp = sParm;
				sParm = eParm;
				eParm = temp;
			}
			isoEllipse.MakeBound(sParm, eParm);
			
			if(!isoEllipse.Evaluate(0.5, true).IsAlmostEqualTo(mPoint, 0.0000000000001)) //check to make sure the ellipse is in the proper direction
			{
				isoEllipse.MakeUnbound();
				isoEllipse.MakeBound(eParm - Math.PI*2, sParm);
			}
			
			return isoEllipse;
		}
		
		private Ellipse transformEllipseArc(Ellipse theEllipse, bool left_face)
		{
			if(!theEllipse.IsBound)
				throw new ArgumentException("transformEllipseArc() received a closed arc.");
			
			double sParm, eParm;
			Ellipse isoEllipse = null;
			XYZ sPoint = theEllipse.GetEndPoint(0);
			XYZ ePoint = theEllipse.GetEndPoint(1);
			XYZ mPoint = theEllipse.Evaluate(0.5, true);
			Ellipse e = Ellipse.CreateCurve(theEllipse.Center, theEllipse.RadiusX, theEllipse.RadiusY, theEllipse.XDirection, theEllipse.YDirection, 0, Math.PI * 2) as Ellipse;
			Line parmLine = Line.CreateBound(sPoint, ePoint);
			
			if(left_face)
			{
				isoEllipse = TransformEllipse_Side(e, true);
				parmLine = isoLine_Left(parmLine);
				mPoint = SSR_point(mPoint, true);
			}
			else
			{
				isoEllipse = TransformEllipse_Side(e, false);
				parmLine = isoLine_Right(parmLine);
				mPoint = SSR_point(mPoint, false);
			}
			
			IntersectionResult ir = isoEllipse.Project(parmLine.GetEndPoint(0));
			sParm = ir.Parameter;
			ir = isoEllipse.Project(parmLine.GetEndPoint(1));
			eParm = ir.Parameter;
			
			if(sParm > eParm)
			{
				double temp = sParm;
				sParm = eParm;
				eParm = temp;
			}
			isoEllipse.MakeBound(sParm, eParm);
			
			if(!isoEllipse.Evaluate(0.5, true).IsAlmostEqualTo(mPoint, 0.00001)) //check to make sure the ellipse is in the proper direction
			{
				isoEllipse.MakeUnbound();
				isoEllipse.MakeBound(eParm - Math.PI*2, sParm);
			}
			
			return isoEllipse;
		}
		#endregion
		
		#region Ellipse functions
		private Ellipse isoEllipse_Right (Ellipse theEllipse)
		{
			return TransformEllipse_Side(theEllipse, false);
		}
		
		private Ellipse isoEllipse_Left (Ellipse theEllipse)
		{
			return TransformEllipse_Side(theEllipse, true);
		}
		
		private Ellipse isoEllipse_Top (Ellipse theEllipse)
		{
			if(theEllipse.IsBound)
				throw new ArgumentException("Transform top ellipse received an open ellipse.");
			
			XYZ centrePoint = theEllipse.Center;
			XYZ minorPoint1 = centrePoint.Add(theEllipse.YDirection.Multiply(theEllipse.RadiusY));
			XYZ minorPoint2 = centrePoint.Subtract(theEllipse.YDirection.Multiply(theEllipse.RadiusY));
			XYZ majorPoint1 = centrePoint.Add(theEllipse.XDirection.Multiply(theEllipse.RadiusX));
			XYZ majorPoint2 = centrePoint.Subtract(theEllipse.XDirection.Multiply(theEllipse.RadiusX));
			
			Line minorLine = Line.CreateBound(minorPoint1, minorPoint2);
			Line majorLine = Line.CreateBound(majorPoint1, majorPoint2);
			
			minorLine = isoLine_Top(minorLine);
			majorLine = isoLine_Top(majorLine);
			
			return from_conjugate_diameters(majorLine, minorLine);
		}
		
		private Ellipse TransformEllipse_Side(Ellipse theEllipse, bool left_face)
		{
			if(theEllipse.IsBound)
				throw new ArgumentException("Transform side ellipse received an open ellipse.");
			
			XYZ centrePoint = theEllipse.Center;
			XYZ minorPoint1 = centrePoint.Add(theEllipse.YDirection.Multiply(theEllipse.RadiusY));
			XYZ minorPoint2 = centrePoint.Subtract(theEllipse.YDirection.Multiply(theEllipse.RadiusY));
			XYZ majorPoint1 = centrePoint.Add(theEllipse.XDirection.Multiply(theEllipse.RadiusX));
			XYZ majorPoint2 = centrePoint.Subtract(theEllipse.XDirection.Multiply(theEllipse.RadiusX));
			
			Line minorLine = Line.CreateBound(minorPoint1, minorPoint2);
			Line majorLine = Line.CreateBound(majorPoint1, majorPoint2);
			
			if(left_face)
			{
				minorLine = isoLine_Left(minorLine);
				majorLine = isoLine_Left(majorLine);
			}
			else
			{
				minorLine = isoLine_Right(minorLine);
				majorLine = isoLine_Right(majorLine);
			}
			
			return from_conjugate_diameters(majorLine, minorLine);
		}
		
		/// <summary>
		/// Find the major and minor axes of an ellipse from a parallelogram
		///determining the conjugate diameters.
		/// 
		///Uses Rytz's construction for algorithm:
		///http://en.wikipedia.org/wiki/Rytz%27s_construction
		/// </summary>
		/// <returns></returns>
		private Ellipse from_conjugate_diameters(Line l1, Line l2)
		{
			XYZ c = l1.Evaluate(0.5, true);
			XYZ u = l1.GetEndPoint(0);
			XYZ v = l2.GetEndPoint(0);
			//TODO: if u and v are perpendicular return new ellipse??
			
			//step #1
			XYZ ur = rotateTowards(u, v, 90, c);
			XYZ s = ((ur - v) / 2.0) + v; //midPoint(ur, v)
			
			//step #2
			//r = rect(np.abs(s), np.angle(ur - s)) + s;
			XYZ r = (ur-s).Normalize().Multiply(s.DistanceTo(c)) + s;
			//l = rect(np.abs(s), np.angle(v - s)) + s;
			XYZ l = (v-s).Normalize().Multiply(s.DistanceTo(c)) + s;
			
			double a = v.DistanceTo(r); //length of major axis
			double b = v.DistanceTo(l); //length of minor axis
			
			XYZ xDirection = (l-c).Normalize();
			XYZ yDirection = (r-c).Normalize();
			
			return Ellipse.CreateCurve(c, a, b, xDirection, yDirection, 0, Math.PI*2) as Ellipse;
		}
		#endregion
		
		#region Line Functions
		private Line isoLine_Top(Line theLine)
		{
			Line a = scaleLine(theLine);
			a = shearLine(a, false);
			a = rotateLine(a, false);
			return a;
		}
		
		private Line isoLine_Left(Line theLine)
		{
			Line a = scaleLine(theLine);
			a = shearLine(a, true);
			a = rotateLine(a, false);
			return a;
		}
		
		private Line isoLine_Right(Line theLine)
		{
			Line a = scaleLine(theLine);
			a = shearLine(a, false);
			a = rotateLine(a, true);
			return a;
		}
		
		private Line shearLine(Line theLine, bool left_face)
		{
			Autodesk.Revit.DB.Transform shearTransform = Autodesk.Revit.DB.Transform.Identity;
			if(left_face)
			{
				shearTransform.BasisY = new XYZ( -Math.Tan(30*(Math.PI/180)), 1, 0); //convert 30 degress to radians
				shearTransform.Origin = new XYZ(origin.Y*(Math.Tan(30*(Math.PI/180))), 0, 0);
			}
			else
			{
				shearTransform.BasisY = new XYZ( Math.Tan(30*(Math.PI/180)), 1, 0);
				shearTransform.Origin = new XYZ(-origin.Y*(Math.Tan(30*(Math.PI/180))), 0, 0);
			}

			XYZ sPoint = shearTransform.OfPoint(theLine.GetEndPoint(0));
			XYZ ePoint = shearTransform.OfPoint(theLine.GetEndPoint(1));
            
            return Line.CreateBound(sPoint, ePoint);
		}
		
		private Line scaleLine(Line theLine) //scales in vertical direction only
		{
			Autodesk.Revit.DB.Transform scaleTransform = Autodesk.Revit.DB.Transform.Identity;
			scaleTransform.BasisY = new XYZ( 0, Math.Sin(60*(Math.PI/180)), 0);
			scaleTransform.Origin = new XYZ( 0, origin.Y*(1-(Math.Sin(60*(Math.PI/180)))), 0);

			XYZ sPoint = scaleTransform.OfPoint(theLine.GetEndPoint(0));
			XYZ ePoint = scaleTransform.OfPoint(theLine.GetEndPoint(1));
			return Line.CreateBound(sPoint, ePoint);
		}
		
		private Line rotateLine(Line theLine, bool left_face)
		{
			Autodesk.Revit.DB.Transform rotateTransform = null;
			
			if(left_face)  //counter-clockwise
				rotateTransform = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, 30*(Math.PI/180), origin);
			else
				rotateTransform = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, -30*(Math.PI/180), origin);
			
			XYZ sPoint = rotateTransform.OfPoint(theLine.GetEndPoint(0));
			XYZ ePoint = rotateTransform.OfPoint(theLine.GetEndPoint(1));
			return Line.CreateBound(sPoint, ePoint);
		}
		#endregion
		
		#region Arc Functions
		private Ellipse isoArc_Right(Arc theArc)
		{
			return transformArc(theArc, false);
		}
		
		private Ellipse isoArc_Left(Arc theArc)
		{
			return transformArc(theArc, true);
		}
		
		private Ellipse isoArc_Top(Arc theArc)
		{
			if(!theArc.IsBound)
				throw new ArgumentException("isoArc_Top() received a closed arc.");
			
			double sParm, eParm;
			Ellipse isoCircle = null;
			XYZ sPoint = theArc.GetEndPoint(0);
			XYZ ePoint = theArc.GetEndPoint(1);
			XYZ mPoint = theArc.Evaluate(0.5, true);
			Arc a = Arc.Create(theArc.Center, theArc.Radius, 0, 2*Math.PI, theArc.XDirection, theArc.YDirection);
			Line parmLine = Line.CreateBound(sPoint, ePoint);
			
			isoCircle = isoCircle_Top(a);
			parmLine = isoLine_Top(parmLine);
			mPoint = SSR_point_Top(mPoint);
			
			IntersectionResult ir = isoCircle.Project(parmLine.GetEndPoint(0));
			sParm = ir.Parameter;
			ir = isoCircle.Project(parmLine.GetEndPoint(1));
			eParm = ir.Parameter;
			
			if(sParm > eParm)
			{
				double temp = sParm;
				sParm = eParm;
				eParm = temp;
			}
			isoCircle.MakeBound(sParm, eParm);
			
			if(!isoCircle.Evaluate(0.5, true).IsAlmostEqualTo(mPoint, 0.00001)) //check to make sure the ellipse is in the proper direction
			{
				isoCircle.MakeUnbound();
				isoCircle.MakeBound(eParm - Math.PI*2, sParm);
			}
			
			return isoCircle;
		}
		
		private Ellipse transformArc(Arc theArc, bool left_face)
		{
			if(!theArc.IsBound)
				throw new ArgumentException("transformArc() received a closed arc.");
			
			double sParm, eParm;
			Ellipse isoCircle = null;
			XYZ sPoint = theArc.GetEndPoint(0);
			XYZ ePoint = theArc.GetEndPoint(1);
			XYZ mPoint = theArc.Evaluate(0.5, true);
			Arc a = Arc.Create(theArc.Center, theArc.Radius, 0, 2*Math.PI, theArc.XDirection, theArc.YDirection);
			Line parmLine = Line.CreateBound(sPoint, ePoint);
			
			if(left_face)
			{
				isoCircle = TransformCircle_Side(a, true);
				parmLine = isoLine_Left(parmLine);
				mPoint = SSR_point(mPoint, true);
			}
			else
			{
				isoCircle = TransformCircle_Side(a, false);
				parmLine = isoLine_Right(parmLine);
				mPoint = SSR_point(mPoint, false);
			}
			
			IntersectionResult ir = isoCircle.Project(parmLine.GetEndPoint(0));
			sParm = ir.Parameter;
			ir = isoCircle.Project(parmLine.GetEndPoint(1));
			eParm = ir.Parameter;
			
			if(sParm > eParm)
			{
				double temp = sParm;
				sParm = eParm;
				eParm = temp;
			}
			isoCircle.MakeBound(sParm, eParm);
			
			if(!isoCircle.Evaluate(0.5, true).IsAlmostEqualTo(mPoint, 0.00001)) //check to make sure the ellipse is in the proper direction
			{
				isoCircle.MakeUnbound();
				isoCircle.MakeBound(eParm - Math.PI*2, sParm);
			}
			
			return isoCircle;
		}
		#endregion
		
		#region Circle functions
		private Ellipse isoCircle_Right (Arc theCircle)
		{
			return TransformCircle_Side(theCircle, false);
		}
		
		private Ellipse isoCircle_Left (Arc theCircle)
		{
			return TransformCircle_Side(theCircle, true);
		}
		
		private Ellipse isoCircle_Top (Arc theCircle)
		{
			if(theCircle.IsBound)
				throw new ArgumentException("isoCircle_Right() received an open arc.");
			
			double x,y,minorRadius,majorRadius;
			XYZ centrePoint;
			
			// SSR point on arc
			XYZ p = new XYZ(0, theCircle.Radius, 0);
			XYZ t = origin;
			origin = XYZ.Zero;
			p = SSR_point_Top(p);
			origin = t;
			y = p.Y;
			x = p.X;
			
			minorRadius = Math.Abs( Math.Sqrt( (Math.Pow(x,2)/Math.Pow(Math.Tan(60*(Math.PI/180)), 2)) + Math.Pow(y,2) ) );
			majorRadius = Math.Tan(60*(Math.PI/180)) * minorRadius;
			
			//SSR centrepoint
			centrePoint = SSR_point_Top(theCircle.Center);
			
			return Ellipse.CreateCurve(centrePoint, majorRadius, minorRadius, XYZ.BasisX, XYZ.BasisY, 0, Math.PI * 2) as Ellipse;
		}
		
		private Ellipse TransformCircle_Side(Arc theCircle, bool left_face)
		{
			//where major axis = 1.7320 the minor axis = 1 or r/R = 0.5774 which is tan30 and R/r = 1.7320 or tan60
			
			if(theCircle.IsBound)
				throw new ArgumentException("isoCircle_Right() received an open arc.");
			
			double x,y,minorRadius,majorRadius;
			XYZ X_axis, Y_axis, centrePoint;
			
			// SSR point on arc
			XYZ p = new XYZ(0, theCircle.Radius, 0);
			XYZ t = origin;
			origin = XYZ.Zero;
			p = SSR_point(p, left_face);
			origin = t;
			y = p.Y * 0.7071067812037;
			x = p.X * 0.7071067812037;
			
			minorRadius = Math.Abs( Math.Sqrt( (Math.Pow(x,2)/Math.Pow(Math.Tan(60*(Math.PI/180)), 2)) + Math.Pow(y,2) ) );
			majorRadius = Math.Tan(60*(Math.PI/180)) * minorRadius;
			
			//rotate circle
			Autodesk.Revit.DB.Transform rt = null;
			if(left_face)
				rt = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, -60*(Math.PI/180), XYZ.Zero);
			else
				rt = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, 60*(Math.PI/180), XYZ.Zero);
			X_axis = rt.OfPoint(XYZ.BasisX);
			Y_axis = rt.OfPoint(XYZ.BasisY);
			
			//SSR centrepoint
			centrePoint = SSR_point(theCircle.Center, left_face);
			
			return Ellipse.CreateCurve(centrePoint, majorRadius, minorRadius, X_axis, Y_axis, 0, Math.PI * 2) as Ellipse;
		}
		#endregion
		
		#region Splines
		private NurbSpline isoSpline_Top(NurbSpline theSpline)
		{
			List<XYZ> controls = new List<XYZ>();
			List<double> weights = new List<double>();
			
			foreach(XYZ p in theSpline.CtrlPoints)
				controls.Add(SSR_point_Top(p));
			foreach(double d in theSpline.Weights)
				weights.Add(d);
			
			return NurbSpline.CreateCurve(controls, weights) as NurbSpline;
		}
		
		private NurbSpline isoSpline_Side(NurbSpline theSpline, bool left_face)
		{
			List<XYZ> controls = new List<XYZ>();
			List<double> weights = new List<double>();
			
			foreach(XYZ p in theSpline.CtrlPoints)
				controls.Add(SSR_point(p, left_face));
			foreach(double d in theSpline.Weights)
				weights.Add(d);
			
			return NurbSpline.CreateCurve(controls, weights) as NurbSpline;
		}
		#endregion
		
		private XYZ rotateTowards(XYZ u , XYZ v, double tau, XYZ centre)
		{
			XYZ res = u - v;
			double sign = 1;
			if(res.X > 0 && res.Y > 0)
				sign = 1;
			else if(res.X < 0 && res.Y > 0)
				sign = -1;
			else if(res.X < 0 && res.Y < 0)
				sign = 1;
			else if(res.X > 0 && res.Y < 0)
				sign = -1;
			
			Transform tr = Transform.CreateRotationAtPoint(XYZ.BasisZ, tau * Math.PI/180 * sign, centre);
			return tr.OfPoint(u);
		}
		
		/// <summary>
		/// Checks to see if two double values are equal to 14 decimal places. To compensate for rounding errors.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		private bool almostEqual(double x, double y)
		{
			double epsilon = Math.Max(Math.Abs(x), Math.Abs(y)) * 1E-14;
			return Math.Abs(x - y) <= epsilon;
		}
		
		private XYZ SSR_point(XYZ p, bool left_face)
		{
			Autodesk.Revit.DB.Transform scaleTransform = Autodesk.Revit.DB.Transform.Identity;
			scaleTransform.BasisY = new XYZ( 0, Math.Sin(60*(Math.PI/180)), 0);
			scaleTransform.Origin = new XYZ( 0, origin.Y*(1-(Math.Sin(60*(Math.PI/180)))), 0);
			p = scaleTransform.OfPoint(p);
			
			Autodesk.Revit.DB.Transform shearTransform = Autodesk.Revit.DB.Transform.Identity;
			if(left_face)
			{
				shearTransform.BasisY = new XYZ( -Math.Tan(30*(Math.PI/180)), 1, 0); //convert 30 degress to radians
				shearTransform.Origin = new XYZ(origin.Y*(Math.Tan(30*(Math.PI/180))), 0, 0);
			}
			else
			{
				shearTransform.BasisY = new XYZ( Math.Tan(30*(Math.PI/180)), 1, 0);
				shearTransform.Origin = new XYZ(-origin.Y*(Math.Tan(30*(Math.PI/180))), 0, 0);
			}
			p = shearTransform.OfPoint(p);
			
			Autodesk.Revit.DB.Transform rotateTransform = null;
			if(!left_face)  //counter-clockwise
				rotateTransform = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, 30*(Math.PI/180), origin);
			else
				rotateTransform = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, -30*(Math.PI/180), origin);
			return rotateTransform.OfPoint(p);
		}
		
		private XYZ SSR_point_Top(XYZ p)
		{
			Autodesk.Revit.DB.Transform scaleTransform = Autodesk.Revit.DB.Transform.Identity;
			scaleTransform.BasisY = new XYZ( 0, Math.Sin(60*(Math.PI/180)), 0);
			scaleTransform.Origin = new XYZ( 0, origin.Y*(1-(Math.Sin(60*(Math.PI/180)))), 0);
			p = scaleTransform.OfPoint(p);
			
			Autodesk.Revit.DB.Transform shearTransform = Autodesk.Revit.DB.Transform.Identity;
			shearTransform.BasisY = new XYZ( Math.Tan(30*(Math.PI/180)), 1, 0);
			shearTransform.Origin = new XYZ(-origin.Y*(Math.Tan(30*(Math.PI/180))), 0, 0);
			p = shearTransform.OfPoint(p);
			
			Autodesk.Revit.DB.Transform rotateTransform = null;
			rotateTransform = Autodesk.Revit.DB.Transform.CreateRotationAtPoint(XYZ.BasisZ, -30*(Math.PI/180), origin);
			return rotateTransform.OfPoint(p);
		}
		
		/// <summary>
		/// Scans through all the curves and makes sure the endpoint of one curves lands on the
		/// startpoint of the next. This is necessary to create filled regions in Revit.
		/// </summary>
		/// <param name="boundaries">The list of Curves</param>
		/// <returns>The ordered list of curves</returns>
		private List<Curve> OrderBoundaries(List<Curve> boundaries)
		{
			List<Curve> NewBoundaries = new List<Curve>();
			Curve cCurve = boundaries[0];
			boundaries.RemoveAt(0);
			NewBoundaries.Add(cCurve);
			int safetyCounter = 0;
			
			int x = 0;
			while(boundaries.Count > 0)
			{
				++safetyCounter;
				Curve nCurve = boundaries[x];
				if(cCurve.GetEndPoint(1).IsAlmostEqualTo(nCurve.GetEndPoint(0), 0.000001)) //if start and end are equal
				{
					NewBoundaries.Add(nCurve);
					cCurve = nCurve;
					boundaries.RemoveAt(x);
					x = 0;
				}
				else if(cCurve.GetEndPoint(1).IsAlmostEqualTo(nCurve.GetEndPoint(1), 0.000001)) //if ends are equal
				{
					nCurve = nCurve.CreateReversed();
					NewBoundaries.Add(nCurve);
					cCurve = nCurve;
					boundaries.RemoveAt(x);
					x = 0;
				}
				else
				{
					x++;
					if(x >= boundaries.Count)
						x = 0;
				}
				if(safetyCounter > 1000)
					throw new Exception("Safety counter exceeded 1000.");
			}
			
			return NewBoundaries;
		}
		
		private void setOrigin()
		{
			System.Collections.IEnumerator e = sel.GetEnumerator();
			e.MoveNext();
			Element el = e.Current as Element;
			
			if(el.GetType() == typeof(Autodesk.Revit.DB.FilledRegion))
			{
				Options o = new Options();
				Autodesk.Revit.DB.GeometryElement ge = el.get_Geometry(o);
				origin = ge.GetBoundingBox().Min;
			}
			else
			{
				LocationCurve lc = el.Location as LocationCurve;
				if(lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Arc))
				{
					Arc a = lc.Curve as Arc;
					if(a.IsBound)
						origin = a.GetEndPoint(0);
					else
						origin = a.Center;
				}
				else if(lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Line))
				{
					Line l = lc.Curve as Line;
					origin = l.GetEndPoint(0);
				}
				else if(lc.Curve.GetType() == typeof(Autodesk.Revit.DB.Ellipse))
				{
					Ellipse a = lc.Curve as Ellipse;
					if(a.IsBound)
						origin = a.GetEndPoint(0);
					else
						origin = a.Center;
				}
			}
		}
	}
}
