using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

namespace PPKml
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		// class-level declarations
		UIWindow window;
		UINavigationController navigationController;
		UIViewController viewController;

		public UIViewController RootViewController { get { return window.RootViewController; } }

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			// create a new window instance based on the screen size
			window = new UIWindow (UIScreen.MainScreen.Bounds);

			viewController = new MapViewController();

			navigationController = new UINavigationController();
			navigationController.PushViewController (viewController, false);

			// If you have defined a view, add it here:
			window.RootViewController = navigationController;

			// make the window visible
			window.MakeKeyAndVisible ();
			
			return true;
		}
	}
}

