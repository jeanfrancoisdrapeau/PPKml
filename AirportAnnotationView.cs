using System;

using UIKit;
using MapKit;
using Foundation;
using CoreGraphics;

namespace PPKml
{
	public class AirportAnnotationView : MKAnnotationView
	{
		public AirportAnnotationView (NSObject annotation, string reuseIdentifier) : base (annotation, reuseIdentifier)
		{
			//Console.WriteLine ("PlaneAnnotationView");
			Frame = new CGRect (0, 0, 25, 25);
			BackgroundColor = UIColor.Clear;
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
				var previous = UIApplication.CheckForIllegalCrossThreadCalls;
				UIApplication.CheckForIllegalCrossThreadCalls = false;
				var annot = base.Annotation;
				UIApplication.CheckForIllegalCrossThreadCalls = previous;
				return annot;
			}

			set {
				base.Annotation = value;

				SetNeedsDisplay ();
			}
		}
	}
}

