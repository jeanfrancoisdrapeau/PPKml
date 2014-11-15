using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;

using UIKit;
using MapKit;
using Foundation;
using CoreGraphics;
using CoreLocation;

namespace PPKml
{
	public class PlaneDetailsView : UIView
	{
		private PlaneAnnotation _Annotation;
		private UIImageView _PlaneImageView;
		private UIImageView _FromFlagImageView;
		private UIImageView _ToFlagImageView;

		string _cachePicture = string.Empty;

		WebRequest req = null;
		WebRequestEx.WebRequestState reqState = null;

		public PlaneAnnotation SetAnnotation { 
			set { 
				_Annotation = value;

				if (_Annotation != null) {
					// Airplane type
					string ptype = _Annotation._PType.ToUpper ().Trim ();

					string oicao = string.Empty;
					if (_Annotation._RouteDetail.OperatorIcao != null)
						oicao = _Annotation._RouteDetail.OperatorIcao.ToUpper ().Trim ();

					// Combine both
					string pictureName = oicao + "_" + ptype + ".jpg";

					var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
					_cachePicture = Path.Combine (documents, "..", "Library", "Caches", pictureName);

					if (!File.Exists (_cachePicture)) {

						//Uri fileURI = new Uri (@"http://www.antonakis.co.uk/sbsflags/jpgimages/" + pictureName);
						Uri fileURI = new Uri (
							string.Format(@"http://planefinder.net/flightstat/v1/getImage.php?airlineCode={0}&aircraftType={1}&skipFuzzy=1", 
								oicao, ptype));

						req = (HttpWebRequest)HttpWebRequest.Create (fileURI);
						reqState = new WebRequestEx.HttpWebRequestState (WebRequestEx.BUFFER_SIZE);
						reqState.request = req;

						if (req != null) {
							reqState.fileURI = fileURI;
							reqState.doneCB = new WebRequestEx.DoneDelegate (DownloadDelegate);
							reqState.transferStart = DateTime.Now;

							// Start the asynchronous request.
							IAsyncResult result = (IAsyncResult)req.BeginGetResponse (
								                     new AsyncCallback (WebRequestEx.RespCallback), reqState);
							ThreadPool.RegisterWaitForSingleObject (
								result.AsyncWaitHandle, 
								new WaitOrTimerCallback (TimeoutCallback), 
								req, 
								5000, 
								true);
						}
					}
				}

				SetNeedsDisplay ();
			}
		}

		public bool IsAnnotationNull {
			get {
				return _Annotation == null;
			}
		}

		public string GetHex {
			get {
				return _Annotation._Hex;
			}
		}
			
		public PlaneDetailsView ()
		{
			_PlaneImageView = new UIImageView ();
			_PlaneImageView.Frame = new RectangleF(
				85, 
				10, 
				115, 
				34
			);
			_PlaneImageView.Layer.CornerRadius = 5.0f;
			_PlaneImageView.ClipsToBounds = true;
			this.AddSubview (_PlaneImageView);

			_FromFlagImageView = new UIImageView ();
			_FromFlagImageView.Frame = new RectangleF(
				180, 
				62, 
				18, 
				12
			);
			this.AddSubview (_FromFlagImageView);

			_ToFlagImageView = new UIImageView ();
			_ToFlagImageView.Frame = new RectangleF(
				180, 
				73, 
				18, 
				12
			);
			this.AddSubview (_ToFlagImageView);
		}

		public override void Draw (CGRect rect)
		{
			base.Draw (rect);

			if (_Annotation == null)
				return;

			using (var context = UIGraphics.GetCurrentContext ()) {

				// Line1, FLight, Hex and Last seen
				CGRect trect = new CGRect (rect.X + 5, rect.Y, rect.Width, 9);
				NSString text = new NSString (
					string.Format("Flight: {0}", (_Annotation._Flight.Length == 0) ? "N/A" : _Annotation._Flight)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				trect = new CGRect (rect.Width / 2, rect.Y, rect.Width, 9);
				text = new NSString (
					string.Format("Hex: {0}", (_Annotation._Hex.Length == 0) ? "N/A" : _Annotation._Hex)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				/*Planes.PlaneDetails pd = Planes.PlanesList.Find (p => p._Hex == _Annotation._Hex);
				if (pd != null) {
					TimeSpan ts = DateTime.Now - pd._LastSeen;
					trect = new CGRect (170, rect.Y, rect.Width, 9);
					text = new NSString (
						string.Format ("({0:00}:{1:00})", ts.TotalMinutes, ts.TotalSeconds)
					);
					UIColor.White.SetColor ();
					text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
					text.Dispose ();
				}*/

				// Line2, Callsign and Type
				trect = new CGRect (rect.X + 5, rect.Y + 9, rect.Width, 9);
				text = new NSString (
					string.Format("Callsign: {0}", (_Annotation._PCode.Length == 0) ? "N/A" : _Annotation._PCode)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				trect = new CGRect (rect.Width / 2, rect.Y + 9, rect.Width, 9);
				text = new NSString (
					string.Format("Type: {0}", (_Annotation._PType.Length == 0) ? "N/A" : _Annotation._PType)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();
				
				// Line3, Altitude, Picture
				trect = new CGRect (rect.X + 5, rect.Y + 18, rect.Width, 9);
				text = new NSString (
					string.Format("Altitude: {0}ft", _Annotation._Level.ToString())
				);
				if (_Annotation._ChangingElevation == PlaneAnnotation.ChangingElevation.Climbing)
					UIColor.Green.SetColor ();
				else if (_Annotation._ChangingElevation == PlaneAnnotation.ChangingElevation.Descending)
					UIColor.Orange.SetColor ();
				else
					UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				if (File.Exists (_cachePicture)) {
					_PlaneImageView.Image = UIImage.FromFile (_cachePicture);
				} else
					_PlaneImageView.Image = null;

				// Line 4, Heading
				trect = new CGRect (rect.X + 5, rect.Y + 27, rect.Width, 9);
				text = new NSString (
					string.Format("Heading: {0}°", _Annotation._Heading.ToString())
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				// Line 5, Speed
				trect = new CGRect (rect.X + 5, rect.Y + 36, rect.Width, 9);
				text = new NSString (
					string.Format("Speed: {0:0.0}km/h", 
						_Annotation._Speed * 1.852
					)
				);
				if (_Annotation._ChangingSpeed == PlaneAnnotation.ChangingSpeed.Accelerating)
					UIColor.Green.SetColor ();
				else if (_Annotation._ChangingSpeed == PlaneAnnotation.ChangingSpeed.Decelerating)
					UIColor.Orange.SetColor ();
				else
					UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				// Line 6, Coordinates
				trect = new CGRect (rect.X + 5, rect.Y + 45, rect.Width, 9);
				text = new NSString (
					string.Format("Coordinates: {0:0.0000}, {1:0.0000} ({2})", 
						_Annotation.Coordinate.Longitude,
						_Annotation.Coordinate.Latitude,
						_Annotation._IsFaa ? "Calculated" : 
							(_Annotation._Heading == 0 && _Annotation._Speed == 0) ? "M-LAT" : "Real-time")
				);
				if (_Annotation._SameCoord)
					UIColor.Orange.SetColor ();
				else
					UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				// Line 7, Route
				double distance = 0;
				if (_Annotation._RouteDetail.ToAirportLat != 0 &&
					_Annotation._RouteDetail.ToAirportLong != 0) 
				{
					distance = GetDistanceFromLatLonInKm (
					                  _Annotation.Coordinate.Latitude,
					                  _Annotation.Coordinate.Longitude,
					                  _Annotation._RouteDetail.ToAirportLat,
					                  _Annotation._RouteDetail.ToAirportLong);
				}
				trect = new CGRect (rect.X + 5, rect.Y + 54, rect.Width, 9);
				text = new NSString (
					string.Format("Distance: {0:0.0}km - ETA: {1}", 
						distance,
						CalcETA(distance, _Annotation._Speed * 1.852)
					)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				// Line 8, From
				_FromFlagImageView.Image = UIImage.FromBundle ("Flags/" + _Annotation._RouteDetail.FromAirportCountryCode);

				trect = new CGRect (rect.X + 5, rect.Y + 63, rect.Width, 9);
				text = new NSString (
					string.Format("From: ({0}) {1}, {2}",
						string.IsNullOrEmpty(_Annotation._RouteDetail.FromAirportCode) ? "N/A" : _Annotation._RouteDetail.FromAirportCode,
						string.IsNullOrEmpty(_Annotation._RouteDetail.FromAirportCity) ? "N/A" : _Annotation._RouteDetail.FromAirportCity,
						string.IsNullOrEmpty(_Annotation._RouteDetail.FromAirportCountryCode) ? "N/A" : _Annotation._RouteDetail.FromAirportCountryCode.ToUpper()
					)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();

				// Line 9, To
				_ToFlagImageView.Image = UIImage.FromBundle ("Flags/" + _Annotation._RouteDetail.ToAirportCountryCode);

				trect = new CGRect (rect.X + 5, rect.Y + 72, rect.Width, 9);
				text = new NSString (
					string.Format("To: ({0}) {1}, {2}", 
						string.IsNullOrEmpty(_Annotation._RouteDetail.ToAirportCode) ? "N/A" : _Annotation._RouteDetail.ToAirportCode,
						string.IsNullOrEmpty(_Annotation._RouteDetail.ToAirportCity) ? "N/A" : _Annotation._RouteDetail.ToAirportCity,
						string.IsNullOrEmpty(_Annotation._RouteDetail.ToAirportCountryCode) ? "N/A" : _Annotation._RouteDetail.ToAirportCountryCode.ToUpper()
					)
				);
				UIColor.White.SetColor ();
				text.DrawString (trect, UIFont.BoldSystemFontOfSize (9.0f));
				text.Dispose ();
			}
		}

		void DownloadDelegate()
		{
			if (reqState.bufferData == null)
				return;

			FileStream fs = new FileStream (_cachePicture, FileMode.Create);
			fs.Write (reqState.bufferData, 0, reqState.bufferData.Length);
			fs.Flush ();
			fs.Close ();

			InvokeOnMainThread (delegate {
				SetNeedsDisplay ();
			});	
		}

		void TimeoutCallback(object state, bool timedOut)
		{
			if (timedOut) {
				Console.WriteLine("PlaneDetails: HTTP Request timed out");
				HttpWebRequest request = state as HttpWebRequest;
				if (request != null) {
					request.Abort();
				}
			}
		}

		private double GetDistanceFromLatLonInKm (double lat1, double lon1, double lat2, double lon2) 
		{
			CLLocation pointALocation = new CLLocation (lat1, lon1);
			CLLocation pointBLocation = new CLLocation (lat2, lon2);

			return pointALocation.DistanceFrom(pointBLocation) / 1000;
		}

		private string CalcETA(double distance, double speedKmh)
		{
			double hours = 0;

			if (speedKmh > 0)
				hours = distance / speedKmh;

			return ConvertFromDecimalToHHMM((decimal)hours);
		}

		private string ConvertFromDecimalToHHMM(decimal dHours)
		{
			decimal hours = Math.Floor(dHours); //take integral part
			decimal minutes = (dHours - hours) * 60.0M; //multiply fractional part with 60
			int D = (int)Math.Floor(dHours / 24);
			int H = (int)Math.Floor(hours - (D * 24));
			int M = (int)Math.Floor(minutes);
			//int S = (int)Math.Floor(seconds);   //add if you want seconds
			return String.Format("{0:00}:{1:00}", H, M);
		}
	}
}

