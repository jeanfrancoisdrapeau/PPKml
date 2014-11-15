using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Timers;

using CoreLocation;
using CoreGraphics;
using MapKit;
using UIKit;
using Foundation;
using ObjCRuntime;

using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;

using SQLite;

using MonoTouch.Dialog;

using GCDiscreetNotification;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PPKml 
{
	public class MapViewController : UIViewController
	{
		class Airport 
		{
			public string Icao { get; set; }
			public string Location { get; set; }
			public double Longitude { get; set; }
			public double Latitude { get; set; }
			public string Name { get; set; }
		}

		class CoordsAirportRoute
		{
			public double Longitude { get; set; }
			public double Latitude { get; set; }
		}

		class Operator
		{
			public string Icao { get; set; }
		}

		List<AirportAnnotation> airportAnnotationList = new List<AirportAnnotation>();
		List<PlaneAnnotation> planeAnnotationList = new List<PlaneAnnotation> ();
		List<PlaneAnnotation> planeAnnotationListFaa = new List<PlaneAnnotation> ();

		MKMapView mapView;

		System.Timers.Timer _timerUi;
		System.Timers.Timer _timerData;
		System.Timers.Timer _timerDataFaa;

		UISegmentedControl mapTypes;

		NSObject observerBackground;

		double _mapHeading = 0;

		OptionsMonoDialog _optionsDialog;

		bool receivingData = false;
		bool receivingDataFaa = false;
		bool mapInteractionEnabled = true;

		long totalBytes = 0;
		int totalPlanesModes = 0;
		int totalPlanesModesVis = 0;
		int totalPlanesFaa = 0;
		int totalPlanesFaaVis = 0;

		UILabel bottomStats;

		UIBarButtonItem uiButtonPlay;
		UIBarButtonItem uiButtonPause;

		UIBarButtonItem uiButtonOptions;
		//UIBarButtonItem uiButtonFilters;

		const float planeDetailsViewWidth = 200.0f;
		const float planeDetailsViewHeight = 90.0f;
		PlaneDetailsView planeDetailsView;
		UIView planeDetailsViewOutter;

		string _selectedPlaneViewHex;

		WebRequest req = null;
		WebRequestEx.WebRequestState reqState = null;

		WebRequest reqFaa = null;
		WebRequestEx.WebRequestState reqStateFaa = null;

		MKCoordinateRegion _currentRegionForAirports = new MKCoordinateRegion();
		MKCoordinateRegion _currentRegion = new MKCoordinateRegion();

		bool _forceRefresh = false;

		const double MAX_TRAIL_LENGTH = 100000; // Meters (100km)

		Countries _countries = new Countries();

		GCDiscreetNotificationView _notificationView;

		public override void ViewWillDisappear(bool animated)
		{
			//Console.WriteLine ("ViewWillDisappear");

			NSNotificationCenter.DefaultCenter.RemoveObserver(observerBackground);

			base.ViewWillDisappear (animated);
		}
			
		public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
		{
			//Console.WriteLine ("UIInterfaceOrientationMask");

			return UIInterfaceOrientationMask.All;
		}

		public override void WillAnimateRotation (UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			//Console.WriteLine ("WillAnimateRotation");

			bottomStats.Frame = new CGRect (
				0,
				NavigationController.NavigationBar.Bounds.Height + UIApplication.SharedApplication.StatusBarFrame.Height,
				NavigationController.NavigationBar.Bounds.Width,
				10);

			base.WillAnimateRotation (toInterfaceOrientation, duration);
		}

		public override void DidRotate( UIInterfaceOrientation fromInterfaceOrientation )
		{
			//Console.WriteLine ("DidRotate");

			base.DidRotate (fromInterfaceOrientation);
		}
			
		public override void ViewDidLoad ()
		{
			//Console.WriteLine ("ViewDidLoad");
			base.ViewDidLoad ();

			Title = "PPKml";

			_notificationView = new GCDiscreetNotificationView (
				text: "Nothing shown at this zoom level",
				activity: false,
				presentationMode: GCDNPresentationMode.Bottom,
				view: View
			);

			CLLocationManager locationManager = new CLLocationManager ();
			locationManager.RequestWhenInUseAuthorization ();

			mapView = new MKMapView(View.Bounds);
			mapView.AutoresizingMask = UIViewAutoresizing.All;
			mapView.ShowsBuildings = true;
			mapView.ShowsPointsOfInterest = false;
			mapView.ZoomEnabled = true;
			mapView.ScrollEnabled = true;
			mapView.GetViewForAnnotation += GetAnnotationView;
			mapView.OverlayRenderer += OverlayRenderer;
			mapView.RegionChanged += RegionChanged;
			mapView.DidSelectAnnotationView += SelectAnnotationView;
			mapView.AddGestureRecognizer (new UITapGestureRecognizer (TapGestureRecognizer));

			View.AddSubview(mapView);

			_optionsDialog = new OptionsMonoDialog ();
			_optionsDialog._mapView = mapView;

			uiButtonOptions = new UIBarButtonItem (UIImage.FromBundle ("gear"),
				UIBarButtonItemStyle.Plain, (sender, args) => {
				this.NavigationController.PushViewController (_optionsDialog, true);
			});

			/*uiButtonFilters = new UIBarButtonItem (UIBarButtonSystemItem.Organize, (s, e) => {
				this.NavigationController.PushViewController (_filtersTableView, true);
			});*/
					
			this.NavigationItem.SetRightBarButtonItems( new UIBarButtonItem[] {
				uiButtonOptions,
				//uiButtonFilters,
			}, true);

			uiButtonPlay = new UIBarButtonItem (UIBarButtonSystemItem.Play, (s, e) => {
				StartTimers ();
			});

			uiButtonPause = new UIBarButtonItem (UIBarButtonSystemItem.Pause, (s, e) => {
				StopTimers ();
			});
			uiButtonPause.Enabled = false;

			this.NavigationItem.SetLeftBarButtonItems( new UIBarButtonItem[] { 
				uiButtonPlay, 
				uiButtonPause
			}, false);

			observerBackground = NSNotificationCenter.DefaultCenter.AddObserver(
				UIApplication.DidEnterBackgroundNotification, 
				delegate(NSNotification ntf) {
					StopTimers();
				});

			LoadRegionCoords ();

			bottomStats = new UILabel ();
			bottomStats.Text = "Ready!";
			bottomStats.Font = UIFont.FromName ("CourierNewPS-BoldMT", 10);
			bottomStats.TextColor = UIColor.White;
			bottomStats.BackgroundColor = UIColor.DarkGray;
			bottomStats.AutoresizingMask = UIViewAutoresizing.All;
			bottomStats.Frame = new CGRect (
				0,
				NavigationController.NavigationBar.Bounds.Height + UIApplication.SharedApplication.StatusBarFrame.Height,
				NavigationController.NavigationBar.Bounds.Width,
				10);
			View.AddSubview (bottomStats);

			int typesWidth=200, typesHeight=25, distanceFromTop=15;
			mapTypes = new UISegmentedControl(
				new RectangleF((
					(float)View.Bounds.Width-typesWidth)/2, 
					(float)NavigationController.NavigationBar.Bounds.Height + 
					(float)UIApplication.SharedApplication.StatusBarFrame.Height + 
					distanceFromTop, 
					typesWidth, 
					typesHeight));
			mapTypes.InsertSegment("Road", 0, false);
			mapTypes.InsertSegment("Satellite", 1, false);
			mapTypes.InsertSegment("Hybrid", 2, false);
			mapTypes.SelectedSegment = NSUserDefaults.StandardUserDefaults.IntForKey("MapType");
			mapTypes.Layer.CornerRadius = 4.0f;
			mapTypes.ClipsToBounds = true;
			mapTypes.BackgroundColor = UIColor.White;
			mapTypes.AutoresizingMask = (
				UIViewAutoresizing.FlexibleLeftMargin | 
				UIViewAutoresizing.FlexibleRightMargin | 
				UIViewAutoresizing.FlexibleTopMargin | 
				UIViewAutoresizing.FlexibleBottomMargin);
			mapTypes.ValueChanged += (s, e) => {
				switch(mapTypes.SelectedSegment) {
				case 0:
					mapView.MapType = MKMapType.Standard;
					break;
				case 1:
					mapView.MapType = MKMapType.Satellite;
					break;
				case 2:
					mapView.MapType = MKMapType.Hybrid;
					break;
				}

				NSUserDefaults.StandardUserDefaults.SetInt((int)mapView.MapType, "MapType"); 
			};
			View.AddSubview (mapTypes);

			switch(NSUserDefaults.StandardUserDefaults.IntForKey ("MapType")) {
			case 0:
				mapView.MapType = MKMapType.Standard;
				break;
			case 1:
				mapView.MapType = MKMapType.Satellite;
				break;
			case 2:
				mapView.MapType = MKMapType.Hybrid;
				break;
			}

			// planeDetailsView
			planeDetailsView = new PlaneDetailsView ();
			planeDetailsView.Layer.CornerRadius = 5.0f;
			planeDetailsView.Layer.BorderColor = UIColor.Black.CGColor;
			planeDetailsView.Layer.BorderWidth = 1.0f;
			planeDetailsView.Frame = new RectangleF(
				0,
				0, 
				planeDetailsViewWidth, 
				planeDetailsViewHeight
			);
			planeDetailsView.BackgroundColor = UIColor.DarkGray;
			planeDetailsView.Alpha = 1.0f;
			planeDetailsView.ClipsToBounds = true;

			planeDetailsViewOutter = new UIView ();
			planeDetailsViewOutter.Frame = new RectangleF(
				((float)View.Bounds.Width / 2) - (planeDetailsViewWidth / 2), 
				(float)View.Bounds.Height + planeDetailsViewHeight, 
				planeDetailsViewWidth, 
				planeDetailsViewHeight
			);
			planeDetailsViewOutter.AutoresizingMask = (
				UIViewAutoresizing.FlexibleLeftMargin | 
				UIViewAutoresizing.FlexibleRightMargin | 
				UIViewAutoresizing.FlexibleTopMargin);
			planeDetailsViewOutter.Layer.CornerRadius = 5.0f;
			planeDetailsViewOutter.Layer.ShadowOpacity = 1.0f;
			planeDetailsViewOutter.Layer.ShadowPath = UIBezierPath.FromRoundedRect (planeDetailsView.Bounds, 5.0f).CGPath;
			planeDetailsViewOutter.AddSubview (planeDetailsView);
			View.AddSubview (planeDetailsViewOutter);
		}

		void timerElapsedUi (object sender, ElapsedEventArgs e)
		{
			const string functionName = "timerElapsedUi";

			try
			{
				InvokeOnMainThread (delegate {

					bottomStats.Text = string.Format("Downloaded {0} : {1} planes ({2} vis)", 
						BytesToString(totalBytes),
						totalPlanesModes + totalPlanesFaa,
						totalPlanesFaaVis + totalPlanesModesVis
					);
						
					if (!NSUserDefaults.StandardUserDefaults.BoolForKey ("ShowAirports"))
						RemoveAirportsAnnotations ();

					// Show airports
					#region Show airports
					if (_currentRegionForAirports.Center.Latitude != _currentRegion.Center.Latitude ||
						_currentRegionForAirports.Center.Longitude != _currentRegion.Center.Longitude)
					{
						RemoveAirportsAnnotations ();
						_currentRegionForAirports = _currentRegion;
					
						if (NSUserDefaults.StandardUserDefaults.BoolForKey ("ShowAirports")) 
						{
							if ( _currentRegionForAirports.Span.LatitudeDelta <= 10 && _currentRegionForAirports.Span.LongitudeDelta <= 10) 
							{
								// Add airports annotations based on current region
								// and locations in the sqlite db

								string documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
								string localFilenameUnzg = "StandingData.sqb";
								string localPathUnzg;
								localPathUnzg = Path.Combine (documentsPath, localFilenameUnzg);

								double xmin = _currentRegionForAirports.Center.Longitude - _currentRegionForAirports.Span.LongitudeDelta;
								double xmax = _currentRegionForAirports.Center.Longitude + _currentRegionForAirports.Span.LongitudeDelta;
								double ymin = _currentRegionForAirports.Center.Latitude - _currentRegionForAirports.Span.LatitudeDelta;
								double ymax = _currentRegionForAirports.Center.Latitude + _currentRegionForAirports.Span.LatitudeDelta;

								if (File.Exists (localPathUnzg)) {
									string sql = string.Format ("select icao, location, longitude, latitude "
										+ "from airport where longitude >= {0} and longitude <= {1} "
										+ "and latitude >= {2} and latitude <= {3}",
										xmin, xmax, ymin, ymax);

									var conn = new SQLiteConnection (localPathUnzg);
									var listAirports = conn.Query<Airport> (sql);
									conn.Close ();

									//Remove annotations no longer in view
									var newAirportAnnotationList = new List<AirportAnnotation> ();
									foreach (var airport in airportAnnotationList) {
										var oldairport = listAirports.Find (a => a.Icao == airport.Id);
										if (oldairport == null)
											InvokeOnMainThread (delegate {
												mapView.RemoveAnnotation (airport);
											});
										else
											newAirportAnnotationList.Add (airport);
									}

									airportAnnotationList = newAirportAnnotationList;

									foreach (var airport in listAirports) {
										var oldairport = airportAnnotationList.Find (a => a.Id == airport.Icao);
										if (oldairport == null) {
											var airportAnno = new AirportAnnotation (
												airport.Icao,
												new CLLocationCoordinate2D (airport.Latitude, airport.Longitude),
												airport.Icao);

											airportAnnotationList.Add (airportAnno);

											InvokeOnMainThread (delegate {
												mapView.AddAnnotation (airportAnno);
											});				
										}
									}
								}
							} 
						}
					}
					#endregion

					// Remove planes and trails
					#region Remove planes and trails
					var clonedPlaneAnnotationList = new List<PlaneAnnotation>(planeAnnotationList);
					var clonedPlaneAnnotationListFaa = new List<PlaneAnnotation>(planeAnnotationListFaa);

					foreach (MKAnnotation ann in mapView.Annotations)
					{
						if (ann.GetType() == typeof(PlaneAnnotation))
						{
							PlaneAnnotation paAnn = (PlaneAnnotation)ann;

							var foundAnn = clonedPlaneAnnotationList.Find(a => a._Hex == paAnn._Hex);
							var foundAnnFaa = clonedPlaneAnnotationListFaa.Find(a => a._Hex == paAnn._Hex);

							if (foundAnn == null && foundAnnFaa == null)
							{
								mapView.RemoveAnnotation(paAnn);
								if (paAnn.trail != null)
								{
									mapView.RemoveOverlay(paAnn.trail);
								}

								if (_selectedPlaneViewHex == paAnn._Hex)
								{
									_selectedPlaneViewHex = string.Empty;
									planeDetailsView.SetAnnotation = null;

									HidePlaneDetailsView ();
								}
							}
						}
					}
					#endregion

					// Show planes and trails
					#region Show planes and trails
					if (_currentRegion.Span.LatitudeDelta <= 10 && _currentRegion.Span.LongitudeDelta <= 10) 
					{
						_notificationView.Hide (true);

						// ADS-B
						#region ADSB
						foreach (PlaneAnnotation planeAnnotation in clonedPlaneAnnotationList)
						{
							//Console.WriteLine(string.Format("_forceRefresh: {0}", _forceRefresh));

							if (planeAnnotation._NeedsRefresh || _forceRefresh)
							{
								//Console.WriteLine("Map Refreshed");

								planeAnnotation._NeedsRefresh = false;

								if (planeAnnotation._Hex == _selectedPlaneViewHex)
								{
									planeDetailsView.SetAnnotation = planeAnnotation;
									if (NSUserDefaults.StandardUserDefaults.BoolForKey("FollowPlane"))
										mapView.SetCenterCoordinate (planeAnnotation.Coordinate, false);
								}

								foreach (var ann in mapView.Annotations)
								{
									if (ann is PlaneAnnotation)
									{
										PlaneAnnotation p = ann as PlaneAnnotation;
										if (p._Hex == planeAnnotation._Hex)
										{
											mapView.RemoveAnnotation(p);

											if (p.trail != null)
											{
												mapView.RemoveOverlay(p.trail);
											}
										}
									}
								}

								MKMapPoint mp = MKMapPoint.FromCoordinate(planeAnnotation.Coordinate);
								if (mapView.VisibleMapRect.Contains(mp))
								{
									PlaneAnnotation paAnn = new PlaneAnnotation(planeAnnotation);

									mapView.AddAnnotation(paAnn);
									if (NSUserDefaults.StandardUserDefaults.BoolForKey("ShowTrails") && 
										paAnn.coordinatesList.Count > 1)
									{
										paAnn.trail = MKPolyline.FromCoordinates(paAnn.coordinatesList.ToArray());
										mapView.AddOverlay(paAnn.trail);
									}
								}

								//mapView.SetNeedsDisplay();
							}
						}
						#endregion

						#region FAA
						// FAA
						foreach (PlaneAnnotation planeAnnotation in clonedPlaneAnnotationListFaa)
						{
							//Console.WriteLine(string.Format("_forceRefresh: {0}", _forceRefresh));
							PlaneAnnotation foundPlaneAdsb = clonedPlaneAnnotationList.Find(p => p._Hex == planeAnnotation._Hex);
							if (foundPlaneAdsb == null)
							{
								if (planeAnnotation._NeedsRefresh || _forceRefresh)
								{
									//Console.WriteLine("Map Refreshed");

									planeAnnotation._NeedsRefresh = false;

									if (planeAnnotation._Hex == _selectedPlaneViewHex)
									{
										planeDetailsView.SetAnnotation = planeAnnotation;
										if (NSUserDefaults.StandardUserDefaults.BoolForKey("FollowPlane"))
											mapView.SetCenterCoordinate (planeAnnotation.Coordinate, false);
									}

									foreach (var ann in mapView.Annotations)
									{
										if (ann is PlaneAnnotation)
										{
											PlaneAnnotation p = ann as PlaneAnnotation;
											if (p._Hex == planeAnnotation._Hex)
											{
												mapView.RemoveAnnotation(p);

												if (p.trail != null)
												{
													mapView.RemoveOverlay(p.trail);
												}
											}
										}
									}

									CLLocationCoordinate2D virtualPoint = LocationAtDistanceAndBearing(
										planeAnnotation._OriginalCoord,
										planeAnnotation._UnixEpochTime,
										planeAnnotation._Heading,
										planeAnnotation._Speed
									);

									MKMapPoint mp = MKMapPoint.FromCoordinate(virtualPoint);
									if (mapView.VisibleMapRect.Contains(mp))
									{
										PlaneAnnotation paAnn = new PlaneAnnotation(planeAnnotation);
										paAnn.Coordinate = virtualPoint;

										mapView.AddAnnotation(paAnn);
										if (NSUserDefaults.StandardUserDefaults.BoolForKey("ShowTrails") && 
											paAnn.coordinatesList.Count > 1)
										{
											List<CLLocationCoordinate2D> newCoordList = 
												new List<CLLocationCoordinate2D>(paAnn.coordinatesList);

											newCoordList.Add(virtualPoint);

											paAnn.trail = MKPolyline.FromCoordinates(newCoordList.ToArray());
											mapView.AddOverlay(paAnn.trail);
										}
									}

									//mapView.SetNeedsDisplay();
								}
							}
						}
						#endregion
					} 
					else
					{
						RemovePlanesAnnotations ();
						_notificationView.Show (true);
					}

					clonedPlaneAnnotationList = null;
					clonedPlaneAnnotationListFaa = null;

					_forceRefresh = false;

					#endregion
				});
			}
			catch (Exception ex) {
				Console.WriteLine (string.Format("{0}: {1}: {2}", functionName, ex.Source, ex.Message));
			}
		}

		void RemovePlanesAnnotations()
		{
			const string functionName = "RemovePlanesAnnotations";

			try
			{
				foreach (var ann in mapView.Annotations)
				{
					if (ann is PlaneAnnotation)
					{
						PlaneAnnotation p = ann as PlaneAnnotation;

						mapView.RemoveAnnotation(p);

						if (p.trail != null)
						{
							mapView.RemoveOverlay(p.trail);
						}
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine (string.Format("{0}: {1}: {2}", functionName, ex.Source, ex.Message));
			}
		}

		void timerElapsedData (object sender, ElapsedEventArgs e)
		{
			//Console.WriteLine ("timerElapsedData-->Start");

			if (!receivingData) {
				try {
					if (NSUserDefaults.StandardUserDefaults.StringForKey("Url") != null) 
					{
						if (_currentRegion.Span.LatitudeDelta <= 10 && _currentRegion.Span.LongitudeDelta <= 10) 
						{
							receivingData = true;

							Uri fileURI = new Uri (NSUserDefaults.StandardUserDefaults.StringForKey("Url"));

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
									new WaitOrTimerCallback(TimeoutCallback), 
									req, 
									5000, 
									true);
							}					
						}
					}
				} catch (Exception ex) {
					InvokeOnMainThread (delegate {
						bottomStats.Text = ex.Message;
					});

					StopTimers ();
				}
			}
			//Console.WriteLine ("timerElapsedData-->End");
		}

		void timerElapsedDataFaa(object sender, ElapsedEventArgs e)
		{
			if (NSUserDefaults.StandardUserDefaults.BoolForKey ("Faa")) {
				if (!receivingDataFaa) {
					try {
						if (_currentRegion.Span.LatitudeDelta <= 10 && _currentRegion.Span.LongitudeDelta <= 10) {
							receivingDataFaa = true;

							//http://planefinder.net/endpoints/update.php?faa=1&routetype=iata&bounds=45.646437,-77.430048,48.924068,-66.723872

							double bottomLeftLat = _currentRegion.Center.Latitude - _currentRegion.Span.LatitudeDelta;
							double topRightLat = _currentRegion.Center.Latitude + _currentRegion.Span.LatitudeDelta;

							double bottomLeftLong = _currentRegion.Center.Longitude - _currentRegion.Span.LongitudeDelta;
							double topRightLong = _currentRegion.Center.Longitude + _currentRegion.Span.LongitudeDelta;

							Uri fileURI = new Uri (
								             string.Format (@"http://planefinder.net/endpoints/update.php?faa=1&routetype=iata&bounds={0},{1},{2},{3}",
									             bottomLeftLat, bottomLeftLong, topRightLat, topRightLong));

							reqFaa = (HttpWebRequest)HttpWebRequest.Create (fileURI);
							reqStateFaa = new WebRequestEx.HttpWebRequestState (WebRequestEx.BUFFER_SIZE);
							reqStateFaa.request = reqFaa;

							if (reqFaa != null) {
								reqStateFaa.fileURI = fileURI;
								reqStateFaa.doneCB = new WebRequestEx.DoneDelegate (DownloadDelegateFaa);
								reqStateFaa.transferStart = DateTime.Now;

								// Start the asynchronous request.
								IAsyncResult result = (IAsyncResult)reqFaa.BeginGetResponse (
									                     new AsyncCallback (WebRequestEx.RespCallback), reqStateFaa);
								ThreadPool.RegisterWaitForSingleObject (
									result.AsyncWaitHandle, 
									new WaitOrTimerCallback (TimeoutCallbackFaa), 
									reqFaa, 
									5000, 
									true);
							}					
						}
					} catch (Exception ex) {
						InvokeOnMainThread (delegate {
							bottomStats.Text = ex.Message;
						});

						StopTimers ();
					}
				}
			}
		}

		void DownloadDelegate()
		{
			const string functionName = "DownloadDelegate";

			//Console.WriteLine("DownloadDelegate");
			if (reqState.bufferData == null)
				return;

			try
			{
				string result = Encoding.ASCII.GetString(reqState.bufferData);

				Parser parser = new Parser();
				parser.ParseString(result, false);

				//Console.WriteLine ("StartTimer->DownloadStringCompleted->B");

				int downloadTotal = 0;
				int downloadVis = 0;

				Kml kml = parser.Root as Kml;
				if (kml != null)
				{
					foreach (var placemark in kml.Flatten().OfType<SharpKml.Dom.Placemark>())
					{
						if (placemark.Geometry is SharpKml.Dom.Point)
						{
							SharpKml.Dom.Point point = (SharpKml.Dom.Point)placemark.Geometry;
							CLLocationCoordinate2D curCoord = new CLLocationCoordinate2D(
								point.Coordinate.Latitude,
								point.Coordinate.Longitude);

							downloadTotal += 1;

							InvokeOnMainThread(delegate {
								MKMapPoint mp = MKMapPoint.FromCoordinate(curCoord);
								if (mapView.VisibleMapRect.Contains(mp))
									downloadVis += 1;
							});

							if (Math.Round(point.Coordinate.Latitude, 3) != 0 &&
								Math.Round(point.Coordinate.Longitude, 3) != 0)
							{
								//Console.WriteLine ("Parsing plane:\n" + placemark.Description.Text);

								string[] descriptionArray = placemark.Description.Text.Split('\n');

								Dictionary<string, string> descriptionDict = new Dictionary<string, string>();
								for (int i = 0; i < descriptionArray.Count() - 1; i++)
								{
									string[] singleDesc = descriptionArray[i].Split(':');
									descriptionDict.Add(singleDesc[0].Trim(), singleDesc[1].Trim());
								}

								string hex = descriptionDict.ContainsKey("Hex") ? 
									descriptionDict["Hex"] : string.Empty;
								string flight = descriptionDict.ContainsKey("Flight") ? 
									descriptionDict["Flight"] : string.Empty;
								string course = descriptionDict.ContainsKey("Course") ? 
									descriptionDict["Course"] : string.Empty;
								string level = descriptionDict.ContainsKey("Flt Level") ? 
									descriptionDict["Flt Level"] : string.Empty;
								string pcode = descriptionDict.ContainsKey("Reg") ? 
									descriptionDict["Reg"] : string.Empty;
								string ptype = descriptionDict.ContainsKey("Type") ? 
									descriptionDict["Type"] : string.Empty;
								string speed = descriptionDict.ContainsKey("Speed") ? 
									descriptionDict["Speed"] : string.Empty;
									
								descriptionDict = null;
								descriptionArray = null;

								if (!string.IsNullOrEmpty(hex))
								{
									Regex regex = new Regex(@"(\d+)");
									Match match;

									double planeHeading = 0;
									if (course.Length != 0)
									{
										try {
											match = regex.Match(course);
											planeHeading = double.Parse(match.Groups[0].Value);
										} catch {planeHeading = 0;}
									}

									double planeSpeed = 0;
									if (speed.Length != 0)
									{
										try {
											match = regex.Match(speed);
											planeSpeed = double.Parse(match.Groups[0].Value);
										} catch {planeSpeed = 0;}
									}

									double planeLevel = 0;
									if (level.Length != 0)
									{
										try {
											match = regex.Match(level);
											planeLevel = double.Parse(match.Groups[0].Value);
											if (level[0] == 'F')
												planeLevel *= 100;
										} catch {planeLevel = 0;}
									}

									PlaneAnnotation foundPlane = planeAnnotationList.Find(p => p._Hex == hex);

									if (foundPlane == null)
									{
										Console.WriteLine ("New plane hex: " + hex);

										PlaneAnnotation plane = new PlaneAnnotation(
											false,
											DateTime.Now,
											placemark, 
											_mapHeading, 
											planeHeading, 
											flight, 
											planeLevel, 
											hex,
											pcode,
											ptype,
											planeSpeed,
											0,
											curCoord,
											GetRouteDetail(flight));
										plane.coordinatesList.Add(curCoord);
										planeAnnotationList.Add(plane);

										plane = null;
									}
									else
									{
										//Console.WriteLine ("Updated plane hex: " + hex);

										foundPlane._LastSeen = DateTime.Now;
										foundPlane._NeedsRefresh = true;

										if (foundPlane.Coordinate.Latitude == point.Coordinate.Latitude &&
											foundPlane.Coordinate.Latitude == point.Coordinate.Latitude)
											foundPlane._SameCoord = true;
										else
											foundPlane._SameCoord = false;

										if (foundPlane._Speed < planeSpeed)
											foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Accelerating;
										else if (foundPlane._Speed > planeSpeed)
											foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Decelerating;
										else
											foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Neutral;

										if (foundPlane._Level < planeLevel)
											foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Climbing;
										else if (foundPlane._Level > planeLevel)
											foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Descending;
										else
											foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Neutral;

										foundPlane._Placemark = placemark;
										foundPlane._Flight = flight;
										foundPlane._Level = planeLevel;
										foundPlane._MapHeading = _mapHeading;
										foundPlane._Heading = planeHeading;
										foundPlane._PCode = pcode;
										foundPlane._PType = ptype;
										foundPlane._Speed = planeSpeed;
										foundPlane._RouteDetail = GetRouteDetail(flight);

										foundPlane.Coordinate = curCoord;

										foundPlane._OriginalCoord = curCoord;

										foundPlane.coordinatesList.Add(curCoord);

										/*int interval = 5;
										if (_entryIntervalString != null && _entryIntervalString.Length > 0)
											interval = int.Parse (_entryIntervalString);

										int maxPoints = 150 / interval;
										if (foundPlane.coordinatesList.Count > maxPoints)
											foundPlane.coordinatesList.RemoveAt(0);*/

										foreach(var loc in foundPlane.coordinatesList)
										{
											if (loc.Latitude == 0 && loc.Longitude == 0)
												foundPlane.coordinatesList.Remove(loc);
										}
									}

									foundPlane = null;
								}

								point = null;
							}
							else
							{
								//downloadNopos += 1;
							}

							// Remove planes > 1 minute old
							List<PlaneAnnotation> oldPlanes = planeAnnotationList.FindAll(p => p._LastSeen.AddMinutes(1) < DateTime.Now);
							foreach (PlaneAnnotation p in oldPlanes)
							{
								Console.WriteLine ("Purged plane hex: " + p._Hex);

								planeAnnotationList.Remove(p);
							}

							oldPlanes = null;
						}
					}
				}

				//Console.WriteLine ("StartTimer->DownloadStringCompleted->END");
				totalBytes += System.Text.ASCIIEncoding.Unicode.GetByteCount(result);

				totalPlanesModes = downloadTotal;
				totalPlanesModesVis = downloadVis;

				kml = null;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
			finally {
				receivingData = false;
			}
		}

		void DownloadDelegateFaa()
		{
			const string functionName = "DownloadDelegateFaa";

			if (reqStateFaa.bufferData == null)
				return;

			try
			{
				var clonedPlaneAnnotationList = new List<PlaneAnnotation>(planeAnnotationList);

				string result = Encoding.ASCII.GetString(reqStateFaa.bufferData);

				int downloadTotal = 0;
				int downloadVis = 0;

				JObject obj = JObject.Parse(result);

				foreach (JToken child in obj.Children())
				{
					foreach (JToken grandChild in child)
					{
						foreach (JToken grandGrandChild in grandChild)
						{
							foreach (JToken greatGrandGrandChild in grandGrandChild)
							{
								var property = greatGrandGrandChild as JProperty;
								if (property != null)
								{
									string hex = property.Name;

									PlaneAnnotation foundPlaneAdsb = clonedPlaneAnnotationList.Find(p => p._Hex == hex);
									if (foundPlaneAdsb == null)
									{
										foreach (JToken greatGreatGrandGrandChild in greatGrandGrandChild)
										{
											var values = greatGreatGrandGrandChild as JArray;

											SharpKml.Dom.Point point = new SharpKml.Dom.Point();
											point.Coordinate = new Vector(double.Parse(values[3].ToString()), double.Parse(values[4].ToString()));

											CLLocationCoordinate2D curCoord = new CLLocationCoordinate2D(
												point.Coordinate.Latitude,
												point.Coordinate.Longitude);
												
											Placemark placemark = new Placemark();
											placemark.Geometry = point;

											string ptype = values[0].ToString();
											string pcode = values[1].ToString();
											string flight = values[2].ToString();
											string level = values[5].ToString();
											string course = values[6].ToString();
											string speed = values[7].ToString();

											string time = values[8].ToString();
											string operatoricao = values[9].ToString();
											string fromto = values[10].ToString();

											if (!string.IsNullOrEmpty(hex))
											{
												Regex regex = new Regex(@"(\d+)");
												Match match;

												double planeHeading = 0;
												if (course.Length != 0)
												{
													try {
														match = regex.Match(course);
														planeHeading = double.Parse(match.Groups[0].Value);
													} catch {planeHeading = 0;}
												}

												double planeSpeed = 0;
												if (speed.Length != 0)
												{
													try {
														match = regex.Match(speed);
														planeSpeed = double.Parse(match.Groups[0].Value);
													} catch {planeSpeed = 0;}
												}

												double planeLevel = 0;
												if (level.Length != 0)
												{
													try {
														match = regex.Match(level);
														planeLevel = double.Parse(match.Groups[0].Value);
														if (level[0] == 'F')
															planeLevel *= 100;
													} catch {planeLevel = 0;}
												}
												double epochTime = 0;
												if (time.Length != 0)
												{
													try {
														match = regex.Match(time);
														epochTime = double.Parse(match.Groups[0].Value);
													} catch {epochTime = 0;}
												}

												PlaneAnnotation foundPlane = planeAnnotationListFaa.Find(p => p._Hex == hex);

												if (foundPlane == null)
												{
													Console.WriteLine ("New plane hex FAA: " + hex);

													PlaneAnnotation plane = new PlaneAnnotation(
														true,
														DateTime.Now,
														placemark, 
														_mapHeading, 
														planeHeading, 
														flight, 
														planeLevel,
														hex,
														pcode,
														ptype,
														planeSpeed,
														epochTime,
														curCoord,
														GetRouteDetailFaa(operatoricao, fromto)
													);
													plane.coordinatesList.Add(curCoord);
													planeAnnotationListFaa.Add(plane);

													plane = null;
												}
												else
												{
													//Console.WriteLine ("Updated plane hex: " + hex);

													foundPlane._LastSeen = DateTime.Now;
													foundPlane._NeedsRefresh = true;

													foundPlane._SameCoord = false;
													if (foundPlane.Coordinate.Latitude == point.Coordinate.Latitude &&
														foundPlane.Coordinate.Latitude == point.Coordinate.Latitude)
														foundPlane._SameCoord = true;

													if (foundPlane._Speed < planeSpeed)
														foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Accelerating;
													else if (foundPlane._Speed > planeSpeed)
														foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Decelerating;
													else
														foundPlane._ChangingSpeed = PlaneAnnotation.ChangingSpeed.Neutral;

													if (foundPlane._Level < planeLevel)
														foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Climbing;
													else if (foundPlane._Level > planeLevel)
														foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Descending;
													else
														foundPlane._ChangingElevation = PlaneAnnotation.ChangingElevation.Neutral;

													foundPlane._Placemark = placemark;
													foundPlane._Flight = flight;
													foundPlane._Level = planeLevel;
													foundPlane._MapHeading = _mapHeading;
													foundPlane._Heading = planeHeading;
													foundPlane._PCode = pcode;
													foundPlane._PType = ptype;
													foundPlane._Speed = planeSpeed;
													foundPlane._UnixEpochTime = epochTime;
													foundPlane._RouteDetail = GetRouteDetailFaa(operatoricao, fromto);

													//foundPlane.Coordinate = curCoord;
													foundPlane._OriginalCoord = curCoord;

													CLLocationCoordinate2D virtualPoint = LocationAtDistanceAndBearing(
														foundPlane._OriginalCoord,
														foundPlane._UnixEpochTime,
														foundPlane._Heading,
														foundPlane._Speed
													);
													InvokeOnMainThread(delegate {
														MKMapPoint mp = MKMapPoint.FromCoordinate(virtualPoint);
														if (mapView.VisibleMapRect.Contains(mp))
															downloadVis += 1;
													});

													if (!foundPlane._SameCoord)
														foundPlane.coordinatesList.Add(curCoord);

													/*int interval = 5;
													if (_entryIntervalString != null && _entryIntervalString.Length > 0)
														interval = int.Parse (_entryIntervalString);

													int maxPoints = 150 / interval;
													if (foundPlane.coordinatesList.Count > maxPoints)
														foundPlane.coordinatesList.RemoveAt(0);*/

													foreach(var loc in foundPlane.coordinatesList)
													{
														if (loc.Latitude == 0 && loc.Longitude == 0)
															foundPlane.coordinatesList.Remove(loc);
													}
												}

												foundPlane = null;
												point = null;
												placemark = null;
											}

											downloadTotal += 1;
										}
									}

									foundPlaneAdsb = null;
								}
							}
						}
					}
				}

				totalBytes += System.Text.ASCIIEncoding.Unicode.GetByteCount(result);
				totalPlanesFaa = downloadTotal;
				totalPlanesFaaVis = downloadVis;

				// Remove planes > 1 minute old
				List<PlaneAnnotation> oldPlanes = planeAnnotationListFaa.FindAll(p => p._LastSeen.AddMinutes(1) < DateTime.Now);
				foreach (PlaneAnnotation p in oldPlanes)
				{
					Console.WriteLine ("Purged plane hex FAA: " + p._Hex);

					planeAnnotationListFaa.Remove(p);
				}

				clonedPlaneAnnotationList = null;
				oldPlanes = null;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
			finally {
				receivingDataFaa = false;
			}
		}

		CLLocationCoordinate2D LocationAtDistanceAndBearing(
			CLLocationCoordinate2D coordinate, 
			double epochtime, 
			double bearing, 
			double speed)
		{
			const string functionName = "LocationAtDistanceAndBearing";

			CLLocationCoordinate2D virtualCoord = new CLLocationCoordinate2D(0,0);

			try
			{
				const int R = 6371000;

				System.TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
				double secondsSinceEpoch = t.TotalSeconds;

				double timeLapse = (secondsSinceEpoch - epochtime); //+ 300 (? add another 5mins???);
				double speedMs = speed * 0.277778; //from km/h to m/s
				double distance = speedMs * timeLapse;

				// Bearing needs to be in radians
				double bearingRad = bearing * Math.PI / 180;
				double dOverR = distance/R;
				// Lat and lon need to be radians, too
				double currentLat = coordinate.Latitude * Math.PI / 180;
				double currentLon = coordinate.Longitude * Math.PI / 180;

				double newLat = Math.Asin(Math.Sin(currentLat)*Math.Cos(dOverR) + 
					Math.Cos(currentLat)*Math.Sin(dOverR)*Math.Cos(bearingRad));
				double newLon = currentLon + Math.Atan2(Math.Sin(bearingRad)*Math.Sin(dOverR)*Math.Cos(currentLat), 
					Math.Cos(dOverR) - Math.Sin(currentLat)*Math.Sin(newLat));

				// Convert back to degrees for the CLLocation
				virtualCoord = new CLLocationCoordinate2D(newLat * 180 / Math.PI, newLon * 180 / Math.PI);
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return virtualCoord;
		}

		void TimeoutCallback(object state, bool timedOut)
		{
			const string functionName = "TimeoutCallback";

			try
			{
				if (timedOut) {
					Console.WriteLine("Kml: HTTP Request timed out");
					HttpWebRequest request = state as HttpWebRequest;
					if (request != null) {
						request.Abort();
					}
					receivingData = false;

					InvokeOnMainThread (delegate {
						bottomStats.Text = "HTTP Request timed out";
					});
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		void TimeoutCallbackFaa(object state, bool timedOut)
		{
			const string functionName = "TimeoutCallbackFaa";

			try
			{
				if (timedOut) {
					Console.WriteLine("FAA: HTTP Request timed out");
					HttpWebRequest request = state as HttpWebRequest;
					if (request != null) {
						request.Abort();
					}
					receivingDataFaa = false;

					InvokeOnMainThread (delegate {
						bottomStats.Text = "FAA HTTP Request timed out";
					});
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		void StartTimers ()
		{
			const string functionName = "StartTimers";

			try
			{
				if (_optionsDialog._entryUrl.Value == null || _optionsDialog._entryUrl.Value.Length == 0) {
					bottomStats.Text = "Please provide a valid Url.";
					return;
				}

				uiButtonPause.Enabled = true;
				uiButtonPlay.Enabled = false;

				bottomStats.Text = "Connecting...";

				_selectedPlaneViewHex = string.Empty;

				planeAnnotationList = new List<PlaneAnnotation> ();
				foreach (var anno in mapView.Annotations) {
					if (anno is PlaneAnnotation) {

						var paAnn = anno as PlaneAnnotation;
						if (paAnn.trail != null)
							mapView.RemoveOverlay (paAnn.trail);
						paAnn = null;

						mapView.RemoveAnnotation (anno);
					}
				}

				totalBytes = 0;

				_timerUi = new System.Timers.Timer ();
				_timerUi.Interval = 100;
				_timerUi.Elapsed += new System.Timers.ElapsedEventHandler(timerElapsedUi);
				_timerUi.Start ();

				int interval = 5;
				if (NSUserDefaults.StandardUserDefaults.StringForKey("Interval") != null && 
					NSUserDefaults.StandardUserDefaults.StringForKey("Interval").Length > 0)
					interval = int.Parse (NSUserDefaults.StandardUserDefaults.StringForKey("Interval"));

				_timerData = new System.Timers.Timer ();
				_timerData.Interval = interval * 1000;
				_timerData.Elapsed += new System.Timers.ElapsedEventHandler(timerElapsedData);
				_timerData.Start ();

				_timerDataFaa = new System.Timers.Timer ();
				_timerDataFaa.Interval = interval * 1000;
				_timerDataFaa.Elapsed += new System.Timers.ElapsedEventHandler(timerElapsedDataFaa);
				_timerDataFaa.Start ();
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		void StopTimers ()
		{
			const string functionName = "StopTimers";

			try
			{
				//Console.WriteLine ("StopTimer");

				uiButtonPause.Enabled = false;
				uiButtonPlay.Enabled = true;

				if (_timerUi != null) {
					_timerUi.Stop ();
					_timerUi.Dispose ();
					_timerUi = null;
				}

				if (_timerData != null) {
					_timerData.Stop ();
					_timerData.Dispose ();
					_timerData = null;
				}

				if (_timerDataFaa != null) {
					_timerDataFaa.Stop ();
					_timerDataFaa.Dispose ();
					_timerDataFaa = null;
				}

				receivingData = false;
				receivingDataFaa = false;

				bottomStats.Text = "Paused.";
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		void TapGestureRecognizer(UITapGestureRecognizer recognizer)
		{
			const string functionName = "TapGestureRecognizer";

			try
			{
				Console.WriteLine ("TapGestureRecognizer");

				CGPoint pointInView = recognizer.LocationInView(mapView);

				if (_selectedPlaneViewHex != null && !string.IsNullOrEmpty(_selectedPlaneViewHex)) {

					foreach (var annotation in mapView.Annotations) {
						if (annotation is PlaneAnnotation) {

							PlaneAnnotation paAnn = annotation as PlaneAnnotation;
							if (paAnn._Hex == _selectedPlaneViewHex) {

								PlaneAnnotationView paAnnView = mapView.ViewForAnnotation (paAnn) as PlaneAnnotationView;

								if (paAnnView != null) {
									if (!paAnnView.Frame.Contains (pointInView)) {

										_selectedPlaneViewHex = string.Empty;
										planeDetailsView.SetAnnotation = null;

										PlaneAnnotation paList = null;
										if (paAnn._IsFaa)
											paList = planeAnnotationListFaa.First (p => p._Hex == paAnn._Hex);
										else
											paList = planeAnnotationList.First (p => p._Hex == paAnn._Hex);

										if (paList != null)
											paList._NeedsRefresh = true;

										paList = null;

										HidePlaneDetailsView ();
									}
								}
								break;
							}
						}
					}
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		private void ShowPlaneDetailsView()
		{
			const string functionName = "ShowPlaneDetailsView";

			try
			{
				UIView.BeginAnimations (null);
				UIView.SetAnimationDuration (0.2f);
				UIView.SetAnimationCurve (UIViewAnimationCurve.EaseIn);
				planeDetailsViewOutter.Frame = new RectangleF(
					((float)View.Bounds.Width / 2) - (planeDetailsViewWidth / 2), 
					(float)View.Bounds.Height - planeDetailsViewHeight + 5, 
					planeDetailsViewWidth, 
					planeDetailsViewHeight
				);
				UIView.CommitAnimations ();

				if (NSUserDefaults.StandardUserDefaults.BoolForKey("FollowPlane"))
					SetMapInteraction (false);
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		private void HidePlaneDetailsView()
		{
			const string functionName = "HidePlaneDetailsView";

			try
			{
				UIView.BeginAnimations (null);
				UIView.SetAnimationDuration (0.2f);
				UIView.SetAnimationCurve (UIViewAnimationCurve.EaseOut);
				planeDetailsViewOutter.Frame = new RectangleF (
					((float)View.Bounds.Width / 2) - (planeDetailsViewWidth / 2), 
					(float)View.Bounds.Height + planeDetailsViewHeight, 
					planeDetailsViewWidth, 
					planeDetailsViewHeight + 50
				);
				UIView.CommitAnimations ();

				SetMapInteraction (true);
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		private void SelectAnnotationView(object sender, MKAnnotationViewEventArgs e)
		{
			const string functionName = "SelectAnnotationView";

			try
			{
				Console.WriteLine ("SelectAnnotationView");

				var view = e.View;
				if (view is PlaneAnnotationView) {

					PlaneAnnotationView paAnnView = view as PlaneAnnotationView;
					PlaneAnnotation paAnn = paAnnView.Annotation as PlaneAnnotation;

					if (NSUserDefaults.StandardUserDefaults.BoolForKey("FollowPlane"))
						mapView.SetCenterCoordinate (paAnn.Coordinate, true);

					planeDetailsView.SetAnnotation = paAnn;
					_selectedPlaneViewHex = paAnn._Hex;

					PlaneAnnotation paList = null;

					if (paAnn._IsFaa)
						paList = planeAnnotationListFaa.First (p => p._Hex == paAnn._Hex);
					else
						paList = planeAnnotationList.First (p => p._Hex == paAnn._Hex);

					if (paList != null)
						paList._NeedsRefresh = true;

					paList = null;
					paAnn = null;

					ShowPlaneDetailsView ();
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		MKOverlayRenderer OverlayRenderer (MKMapView mapView, IMKOverlay overlay)
		{
			const string functionName = "OverlayRenderer";

			try
			{
				if (overlay is MKPolyline)
				{
					MKPolylineRenderer p = new MKPolylineRenderer((MKPolyline)overlay);
					p.LineWidth = 1.0f;
					p.StrokeColor = UIColor.Blue;
					return p;
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}

		MKAnnotationView GetAnnotationView (MKMapView map, NSObject annotation)
		{	
			const string functionName = "GetAnnotationView";

			try
			{
				//Console.WriteLine ("GetAnnotationView");
				if (annotation is AirportAnnotation) {
					AirportAnnotationView annotationView = (AirportAnnotationView)map.DequeueReusableAnnotation ("airport");
					if (annotationView == null)
						annotationView = new AirportAnnotationView (annotation, "airport");
					else // if we did dequeue one for reuse, assign the annotation to it
						annotationView.Annotation = annotation;

					AirportAnnotation apAnn = (AirportAnnotation)annotation;
					annotationView.Annotation = apAnn;

					annotationView.Image = UIImage.FromBundle ("airport");
					annotationView.CanShowCallout = true;

					return annotationView;
				}

				if (annotation is PlaneAnnotation) {
					NSUserDefaults.StandardUserDefaults.DoubleForKey("CenterMapLong"); 

					PlaneAnnotationView annotationView = (PlaneAnnotationView)map.DequeueReusableAnnotation ("plane");
					if (annotationView == null)
						annotationView = new PlaneAnnotationView (annotation, "plane");
					else // if we did dequeue one for reuse, assign the annotation to it
						annotationView.Annotation = annotation;

					annotationView.CanShowCallout = false;
					//annotationView.CalloutOffset = new CGPoint (0,10);

					PlaneAnnotation paAnn = (PlaneAnnotation)annotation;
					annotationView.Annotation = paAnn;

					bool selectedPlane = false;
					if (!string.IsNullOrEmpty(_selectedPlaneViewHex))
						if (paAnn._Hex == _selectedPlaneViewHex)
							selectedPlane = true;

					//Console.WriteLine(string.Format("IsFaa: {0}", paAnn._IsFaa));

					UIImage img;
					if (paAnn._Heading == 0 && paAnn._Speed == 0)
					{
						if (selectedPlane)
							img = UIImage.FromBundle ("diamond_selected");
						else
							img = UIImage.FromBundle ("diamond");
					}
					else
					{
						if (selectedPlane)
							img = UIImage.FromBundle ("airplane_selected");
						else
						{
							if (paAnn._IsFaa)
								img = UIImage.FromBundle ("airplane_faa");
							else
								img = UIImage.FromBundle ("airplane");
						}

					}
						
					annotationView.Image = RotateImage (img, 
						(float)(paAnn._Heading - paAnn._MapHeading)).Scale (new CGSize (
							(float)img.Size.Width, 
							(float)img.Size.Height), 
							(float)img.CurrentScale);

					/*UIButton detailButton = UIButton.FromType(UIButtonType.DetailDisclosure);
					detailButton.TouchUpInside += (s, e) => {
						new UIAlertView("Details for airplane " + paAnn._Hex, paAnn._Placemark.Description.Text, null, "OK", null).Show();
					};

					annotationView.RightCalloutAccessoryView = detailButton;*/

					return annotationView;
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}

		void RemoveAirportsAnnotations()
		{
			const string functionName = "RemoveAirportsAnnotations";

			try
			{
				if (airportAnnotationList.Count != 0)
				{
					mapView.RemoveAnnotations (airportAnnotationList.ToArray ());
					airportAnnotationList = new List<AirportAnnotation> ();
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		RouteDetail GetRouteDetail(string flight)
		{
			const string functionName = "GetRouteDetail";

			try
			{
				string documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
				string localFilenameUnzg = "StandingData.sqb";
				string localPathUnzg;
				localPathUnzg = Path.Combine (documentsPath, localFilenameUnzg);

				if (File.Exists (localPathUnzg)) {
					string sql = string.Format ("SELECT airport.AirportId, airport.longitude, "
						+ "airport.latitude, airport.icao, airport.location, country.name "
						+ "from airport "
						+ "LEFT JOIN route ON airport.AirportId=route.FromAirportId "
						+ "LEFT JOIN country ON airport.CountryId=country.CountryId "
						+ "where route.Callsign = '" + flight + "'");

					var conn = new SQLiteConnection (localPathUnzg);
					var listFrom = conn.Query<Airport> (sql);

					sql = string.Format ("SELECT airport.AirportId, airport.longitude, "
						+ "airport.latitude, airport.icao, airport.location, country.name "
						+ "from airport "
						+ "LEFT JOIN route ON airport.AirportId=route.ToAirportId "
						+ "LEFT JOIN country ON airport.CountryId=country.CountryId "
						+ "where route.Callsign = '" + flight + "'");

					var listTo = conn.Query<Airport> (sql);

					sql = string.Format ("SELECT operator.Icao FROM operator LEFT JOIN route ON operator.OperatorId=route.OperatorId "
						+ "where route.Callsign = '" + flight + "'");
						
					var listOp = conn.Query<Operator> (sql);

					conn.Close ();

					// Get country code for the flag image
					RouteDetail rd = new RouteDetail ();
					if (listFrom != null && listFrom.Count != 0 && !string.IsNullOrEmpty(listFrom [0].Name))
					{
						Countries.CountryDetails cdFrom = _countries.countriesList.Find (c => c.Country == listFrom [0].Name);

						rd.FromAirportCode = listFrom [0].Icao;
						rd.FromAirportCity = listFrom [0].Location;
						rd.FromAirportCountry = listFrom [0].Name;
						rd.FromAirportCountryCode = cdFrom == null ? string.Empty : cdFrom.Code;
						rd.FromAirportLat = listFrom [0].Latitude;
						rd.FromAirportLong = listFrom [0].Longitude;
					}

					if (listTo != null && listTo.Count != 0 && !string.IsNullOrEmpty(listTo [0].Name))
					{
						Countries.CountryDetails cdTo = _countries.countriesList.Find (c => c.Country == listTo [0].Name);

						rd.ToAirportCode = listTo [0].Icao;
						rd.ToAirportCity = listTo [0].Location;
						rd.ToAirportCountry = listTo [0].Name;
						rd.ToAirportCountryCode = cdTo == null ? string.Empty : cdTo.Code;
						rd.ToAirportLat = listTo [0].Latitude;
						rd.ToAirportLong = listTo [0].Longitude;
					}

					if (listOp != null && listOp.Count != 0)
					{
						rd.OperatorIcao = listOp [0].Icao;
					}

					listFrom = null;
					listTo = null;

					return rd;
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}

		RouteDetail GetRouteDetailFaa(string operatoricao, string fromto)
		{
			const string functionName = "GetRouteDetailFaa";

			try
			{
				string documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
				string localFilenameUnzg = "StandingData.sqb";
				string localPathUnzg;
				localPathUnzg = Path.Combine (documentsPath, localFilenameUnzg);

				if (File.Exists (localPathUnzg)) {

					RouteDetail rd = new RouteDetail ();

					if (fromto.Trim().Length > 0)
					{
						string[] fromToArray = fromto.Split('-');

						if (fromToArray.Count() >= 2)
						{
							string sql = string.Format ("SELECT airport.AirportId, airport.longitude, "
								+ "airport.latitude, airport.icao, airport.location, country.name "
								+ "from airport "
								+ "LEFT JOIN country ON airport.CountryId=country.CountryId "
								+ "where airport.iata = '" + fromToArray[fromToArray.Count() - 2] + "'");

							var conn = new SQLiteConnection (localPathUnzg);
							var listFrom = conn.Query<Airport> (sql);

							sql = string.Format ("SELECT airport.AirportId, airport.longitude, "
								+ "airport.latitude, airport.icao, airport.location, country.name "
								+ "from airport "
								+ "LEFT JOIN country ON airport.CountryId=country.CountryId "
								+ "where airport.iata = '" + fromToArray[fromToArray.Count() - 1] + "'");

							var listTo = conn.Query<Airport> (sql);

							conn.Close ();

							if (listFrom != null && listFrom.Count != 0 && !string.IsNullOrEmpty(listFrom [0].Name))
							{
								Countries.CountryDetails cdFrom = _countries.countriesList.Find (c => c.Country == listFrom [0].Name);

								rd.FromAirportCode = listFrom [0].Icao;
								rd.FromAirportCity = listFrom [0].Location;
								rd.FromAirportCountry = listFrom [0].Name;
								rd.FromAirportCountryCode = cdFrom == null ? string.Empty : cdFrom.Code;
								rd.FromAirportLat = listFrom [0].Latitude;
								rd.FromAirportLong = listFrom [0].Longitude;
							}

							if (listTo != null && listTo.Count != 0 && !string.IsNullOrEmpty(listTo [0].Name))
							{
								Countries.CountryDetails cdTo = _countries.countriesList.Find (c => c.Country == listTo [0].Name);

								rd.ToAirportCode = listTo [0].Icao;
								rd.ToAirportCity = listTo [0].Location;
								rd.ToAirportCountry = listTo [0].Name;
								rd.ToAirportCountryCode = cdTo == null ? string.Empty : cdTo.Code;
								rd.ToAirportLat = listTo [0].Latitude;
								rd.ToAirportLong = listTo [0].Longitude;
							}

							listFrom = null;
							listTo = null;
						}
					}
						
					rd.OperatorIcao = operatoricao;

					return rd;
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}

		static String BytesToString(long byteCount)
		{
			const string functionName = "BytesToString";

			try
			{
				string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
				if (byteCount == 0)
					return "0" + suf[0];
				long bytes = Math.Abs(byteCount);
				int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
				double num = Math.Round(bytes / Math.Pow(1024, place), 1);
				return (Math.Sign(byteCount) * num).ToString() + suf[place];
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}

		/// <summary>
		/// Converts miles to latitude degrees
		/// </summary>
		public double MilesToLatitudeDegrees(double miles)
		{
			const string functionName = "MilesToLatitudeDegrees";

			try
			{
				//Console.WriteLine ("MilesToLatitudeDegrees");

				double earthRadius = 3960.0;
				double radiansToDegrees = 180.0/Math.PI;
				return (miles/earthRadius) * radiansToDegrees;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return 0;
		}

		/// <summary>
		/// Converts miles to longitudinal degrees at a specified latitude
		/// </summary>
		public double MilesToLongitudeDegrees(double miles, double atLatitude)
		{
			const string functionName = "MilesToLongitudeDegrees";

			try
			{
				//Console.WriteLine ("MilesToLongitudeDegrees");

				double earthRadius = 3960.0;
				double degreesToRadians = Math.PI/180.0;
				double radiansToDegrees = 180.0/Math.PI;

				// derive the earth's radius at that point in latitude
				double radiusAtLatitude = earthRadius * Math.Cos(atLatitude * degreesToRadians);
				return (miles / radiusAtLatitude) * radiansToDegrees;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return 0;
		}

		private void SetMapInteraction (bool enabled)
		{
			const string functionName = "SetMapInteraction";

			try
			{
				mapInteractionEnabled = enabled;

				mapView.MultipleTouchEnabled = enabled;
				mapView.PitchEnabled = enabled;
				mapView.RotateEnabled = enabled;
				mapView.ScrollEnabled = enabled;
				//mapView.ZoomEnabled = enabled;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		protected void RegionChanged(object sender, MapKit.MKMapViewChangeEventArgs e)
		{
			const string functionName = "RegionChanged";

			try
			{
				_mapHeading = mapView.Camera.Heading;
				_currentRegion = mapView.Region;
				_forceRefresh = true;

				if (mapInteractionEnabled)
					SaveRegionCoords ();
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		private void SaveRegionCoords()
		{
			const string functionName = "SaveRegionCoords";

			try
			{
				//store
				Console.WriteLine ("SaveRegionCoords");

				MKCoordinateRegion cr = mapView.Region;
				MKCoordinateSpan cs = cr.Span;

				/*Console.WriteLine (string.Format("{0} {1} {2} {3}",
					cr.Center.Longitude,
					cr.Center.Latitude,
					cs.LongitudeDelta, 
					cs.LatitudeDelta));*/

				NSUserDefaults.StandardUserDefaults.SetDouble(mapView.CenterCoordinate.Longitude, "CenterMapLong"); 
				NSUserDefaults.StandardUserDefaults.SetDouble(mapView.CenterCoordinate.Latitude, "CenterMapLat"); 

				NSUserDefaults.StandardUserDefaults.SetDouble(cr.Center.Longitude, "RegMapLong"); 
				NSUserDefaults.StandardUserDefaults.SetDouble(cr.Center.Latitude, "RegMapLat"); 

				NSUserDefaults.StandardUserDefaults.SetDouble(cs.LongitudeDelta, "RegMapLongD"); 
				NSUserDefaults.StandardUserDefaults.SetDouble(cs.LatitudeDelta, "RegMapLatD"); 
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		// Function called from OnCreate
		protected void LoadRegionCoords()
		{
			const string functionName = "LoadRegionCoords";

			try
			{
				//retreive 
				double longitude = NSUserDefaults.StandardUserDefaults.DoubleForKey("CenterMapLong"); 
				double latitude = NSUserDefaults.StandardUserDefaults.DoubleForKey("CenterMapLat");

				double rlongitude = NSUserDefaults.StandardUserDefaults.DoubleForKey("RegMapLong"); 
				double rlatitude = NSUserDefaults.StandardUserDefaults.DoubleForKey("RegMapLat");

				double rlongituded = NSUserDefaults.StandardUserDefaults.DoubleForKey("RegMapLongD"); 
				double rlatituded = NSUserDefaults.StandardUserDefaults.DoubleForKey("RegMapLatD");

				if (longitude != 0 && latitude != 0)
				{
					mapView.SetCenterCoordinate (new CLLocationCoordinate2D(latitude, longitude), true);
				}

				if (rlongitude != 0 && rlatitude != 0)
				{
					mapView.Region = new MKCoordinateRegion (new CLLocationCoordinate2D (rlatitude, rlongitude),
						new MKCoordinateSpan (rlatituded, rlongituded));
				}
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}
		}

		private UIImage RotateImage (UIImage src, float angle)
		{
			const string functionName = "RotateImage";

			try
			{
				UIImage Ret;
				float newSide = Math.Max (src.CGImage.Width, src.CGImage.Height);// * src.CurrentScale;
				SizeF size = new SizeF (newSide, newSide);

				UIGraphics.BeginImageContext (size);
				CGContext context = UIGraphics.GetCurrentContext ();

				context.TranslateCTM (newSide / 2, newSide / 2);

				context.RotateCTM ((float)(angle*Math.PI/180));
				src.Draw (new PointF (-(float)src.Size.Width / 2, -(float)src.Size.Height / 2));
				Ret = UIGraphics.GetImageFromCurrentImageContext ();        

				UIGraphics.EndImageContext ();  // Restore context

				return Ret;
			}
			catch (Exception ex) {
				System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);            
				Console.WriteLine (string.Format("{0}: Line {1}: {2}", functionName, trace.GetFrame(0).GetFileLineNumber(), ex.Message));
			}

			return null;
		}
	}
}