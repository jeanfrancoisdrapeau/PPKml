using System;
using System.Drawing;
using System.Reflection;
using System.Text.RegularExpressions;
using UIKit;
using MapKit;
using Foundation;
using CoreGraphics;

using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;

namespace PPKml
{
	public class PlaneAnnotationView : MKAnnotationView
	{
		const float FrameWidth = 50.0f;
		const float FrameHeight = 56.0f;

		public PlaneAnnotationView (NSObject annotation, string reuseIdentifier) : base (annotation, reuseIdentifier)
		{
			//Console.WriteLine ("PlaneAnnotationView");
			Frame = new CGRect (0, 0, FrameWidth, FrameHeight);
			BackgroundColor = UIColor.Clear;
			CenterOffset = new PointF (0.0f, 3.0f);
		}
			
		public override UIImage Image {
			get {
				return base.Image;
			}
			set {
				CGRect frame = Frame;

				base.Image = value;

				Frame = frame;

				SetNeedsDisplay ();
			}
		}

		public override NSObject Annotation {
			get {

				//Console.WriteLine ("Annotation->get");

				var previous = UIApplication.CheckForIllegalCrossThreadCalls;
				UIApplication.CheckForIllegalCrossThreadCalls = false;
				var annot = base.Annotation;
				UIApplication.CheckForIllegalCrossThreadCalls = previous;
				return annot;
			}

			set {
				//Console.WriteLine ("Annotation->set");
		
				base.Annotation = value;

				// this annotation view has custom drawing code.  So when we reuse an annotation view
				// (through MapView's delegate "dequeueReusableAnnoationViewWithIdentifier" which returns non-nil)
				// we need to have it redraw the new annotation data.
				//
				// for any other custom annotation view which has just contains a simple image, this won't be needed
				//
				SetNeedsDisplay ();
			}
		}

		public override void Draw (CGRect rect)
		{
			//Console.WriteLine ("Draw");

			base.Draw (rect);

			PlaneAnnotation annotation = Annotation as PlaneAnnotation;
			if (annotation == null)
				return;

			// Get the current graphics context
			using (var context = UIGraphics.GetCurrentContext ()) {

				//Console.WriteLine (string.Format("AH: {0}, MH: {1}", annotation._Heading, annotation._MapHeading));
							
				context.SetLineWidth (1.0f);

				// Line1: flight, hex
				// Line2: pcode, ptype
				// Line3: elevation, heading
				// Line4: speed

				// Draw the icon for the weather condition
				UIImage planeImage = UIImage.FromBundle ("airplane");

				this.Image.Draw (new RectangleF (0, 
					0, 
					(float)this.Image.CGImage.Width/(float)planeImage.CurrentScale, 
					(float)this.Image.CGImage.Height/(float)planeImage.CurrentScale));
				planeImage.Dispose ();

				// Draw the bottom rounded box
				CGRect trect = new CGRect (rect.X, rect.Y + 40, rect.Width, 15);
				DrawBox (context, trect, 6.0f);
				context.ClosePath ();
				context.SetFillColor (UIColor.DarkGray.CGColor);
				context.SetStrokeColor (UIColor.Black.CGColor);
				context.DrawPath (CGPathDrawingMode.FillStroke);

				// Line1
				trect = new CGRect (rect.X, rect.Y + 40, rect.Width, 9);
				NSString flightTxt = new NSString ((annotation._Flight.Length == 0) ? "N/A" : annotation._Flight);
				UIColor.White.SetColor ();
				flightTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (7.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				flightTxt.Dispose ();

				// Line2
				trect = new CGRect (rect.X, rect.Y + 46, rect.Width, 9);
				NSString pcodeTxt = new NSString ((annotation._PCode.Length == 0) ? "N/A" : annotation._PCode);
				UIColor.White.SetColor ();
				pcodeTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (7.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				pcodeTxt.Dispose ();

				/*
				trect = new CGRect (rect.Width / 2, rect.Y + 40, 25, 10);
				NSString hexTxt = new NSString (annotation._Hex);
				UIColor.White.SetColor ();
				hexTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				hexTxt.Dispose ();

				// Line2
				trect = new CGRect (rect.X, rect.Y + 45, 25, 10);
				NSString pcodeTxt = new NSString ((annotation._PCode.Length == 0) ? "N/A" : annotation._PCode);
				UIColor.White.SetColor ();
				pcodeTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				pcodeTxt.Dispose ();

				trect = new CGRect (rect.Width / 2, rect.Y + 45, 25, 10);
				NSString ptypeTxt = new NSString ((annotation._PType.Length == 0) ? "N/A" : annotation._PType);
				UIColor.White.SetColor ();
				ptypeTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				ptypeTxt.Dispose ();

				// Line3
				trect = new CGRect (rect.X, rect.Y + 50, 25, 10);
				NSString elevationTxt = new NSString ("F" + annotation._Level.ToString());
				switch (annotation._ChangingElevation)
				{
					case PlaneAnnotation.ChangingElevation.Neutral:
						UIColor.White.SetColor ();
						break;
					case PlaneAnnotation.ChangingElevation.Climbing:
						UIColor.Green.SetColor ();
						break;
					case PlaneAnnotation.ChangingElevation.Descending:
						UIColor.Orange.SetColor ();
						break;
				}
				elevationTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				elevationTxt.Dispose ();

				trect = new CGRect (rect.Width / 2, rect.Y + 50, 25, 10);
				NSString headingTxt = new NSString ("H" + annotation._Heading.ToString());
				UIColor.White.SetColor ();
				headingTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				headingTxt.Dispose ();

				// Line4
				trect = new CGRect (rect.X, rect.Y + 55, 25, 10);
				NSString speedTxt = new NSString ("S" + annotation._Speed.ToString());
				switch (annotation._ChangingSpeed)
				{
					case PlaneAnnotation.ChangingSpeed.Neutral:
						UIColor.White.SetColor ();
						break;
					case PlaneAnnotation.ChangingSpeed.Accelerating:
						UIColor.Green.SetColor ();
						break;
					case PlaneAnnotation.ChangingSpeed.Decelerating:
						UIColor.Orange.SetColor ();
						break;
				}
				speedTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				speedTxt.Dispose ();

				// Line5
				UIColor.White.SetColor ();
				trect = new CGRect (rect.X, rect.Y + 60, 25, 10);
				NSString fromTxt = new NSString (
					(annotation._RouteDetail.FromAirportCode == null) ? "N/A" : annotation._RouteDetail.FromAirportCode
				);
				fromTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				fromTxt.Dispose ();

				trect = new CGRect (rect.Width / 2, rect.Y + 60, 25, 10);
				NSString toTxt = new NSString (
					(annotation._RouteDetail.ToAirportCode == null) ? "N/A" : annotation._RouteDetail.ToAirportCode
				);
				UIColor.White.SetColor ();
				toTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (6.0f), 
					UILineBreakMode.WordWrap, 
					UITextAlignment.Center);
				toTxt.Dispose ();
				*/
				/*

			    trect = new CGRect (rect.X + 5, rect.Y + 48, 20, 10);
				NSString upperLeftTxt = new NSString ((annotation._Level.Length == 0) ? "AN/A" : annotation._Level);
				UIColor.White.SetColor ();
				upperLeftTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (7.0f), 
					UILineBreakMode.WordWrap);
				upperLeftTxt.Dispose ();

				trect = new CGRect (rect.X + 25, rect.Y + 48, 20, 10);
				NSString upperRightTxt = new NSString ("H" + annotation._Heading.ToString());
				UIColor.White.SetColor ();
				upperRightTxt.DrawString (trect, 
					UIFont.BoldSystemFontOfSize (7.0f), 
					UILineBreakMode.WordWrap);
				upperRightTxt.Dispose ();*/
			}
		}

		private void DrawBox(CGContext context, CGRect trect, float fRadius)
		{
			float fWidth = (float)trect.Width;
			float fHeight = (float)trect.Height;

			if (fRadius > fWidth / 2.0f)
			{
				fRadius = fWidth / 2.0f;
			}
			if (fRadius > fHeight / 2.0f)
			{
				fRadius = fHeight / 2.0f;    
			}

			float fMinX = (float)trect.GetMinX ();
			float fMidX = (float)trect.GetMidX ();
			float fMaxX = (float)trect.GetMaxX ();
			float fMinY = (float)trect.GetMinY ();
			float fMidY = (float)trect.GetMidY ();
			float fMaxY = (float)trect.GetMaxY ();

			context.MoveTo (fMinX, fMidY);
			context.AddArcToPoint (fMinX, fMinY, fMidX / 2, fMinY, fRadius);
			context.AddLineToPoint (fMidX - 5, fMinY);
			context.AddLineToPoint (fMidX, fMinY - 5);
			context.AddLineToPoint (fMidX + 5, fMinY);
			context.AddArcToPoint (fMaxX, fMinY, fMaxX, fMidY, fRadius);
			context.AddArcToPoint (fMaxX, fMaxY, fMidX, fMaxY, fRadius);
			context.AddArcToPoint (fMinX, fMaxY, fMinX, fMidY, fRadius);
		}
	}
}

