
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.IO.Compression;

using MonoTouch.Dialog;

using Foundation;
using MapKit;
using UIKit;

namespace PPKml
{
	public partial class OptionsMonoDialog : DialogViewController
	{
		const string BUILD_DATE = "2014/10/29 20:24";

		public EntryElement _entryUrl;
		public EntryElement _entryInterval;
		private StringElement _stringDownload;
		public BooleanElement _boolShowTrails;
		public BooleanElement _boolShowAirports;
		public BooleanElement _boolShowLocation;
		public BooleanElement _boolFollowSelectedPlane;
		public BooleanElement _boolFaa;

		public MKMapView _mapView;

		private string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		private string localFilenameUnzg = "StandingData.sqb";
		private string localPathUnzg;

		public OptionsMonoDialog () : base (UITableViewStyle.Grouped, null, true)
		{
			localPathUnzg = Path.Combine (documentsPath, localFilenameUnzg);

			string fileDownloaded = string.Empty;
			if (File.Exists (localPathUnzg)) {
				FileInfo fi = new FileInfo (localPathUnzg);
				fileDownloaded = string.Format (" ({0})", fi.CreationTime);
			}

			_boolShowLocation = new BooleanElement ("Show current location", false);
			_boolShowLocation.ValueChanged += ShowCurrentLocation;

			Root = new RootElement ("Settings"){
				new Section ("Server") {
					(_entryUrl = new EntryElement ("Url", 
						"http://server:4181/pp_google.kml", NSUserDefaults.StandardUserDefaults.StringForKey("Url"))),
					(_entryInterval = new EntryElement ("Interval", 
						"5", NSUserDefaults.StandardUserDefaults.StringForKey("Interval"))),
					(_boolFaa = new BooleanElement ("Use FAA data", 
						NSUserDefaults.StandardUserDefaults.BoolForKey("Faa"))),
				},
				new Section ("Routes database") {
					(_stringDownload = new StringElement ("Download" + fileDownloaded, DownloadRoutesDb)),
					(_boolShowAirports = new BooleanElement ("Show airports", 
						NSUserDefaults.StandardUserDefaults.BoolForKey("ShowAirports")))
				},
				new Section ("Map") {
					_boolShowLocation,
					(_boolShowTrails = new BooleanElement ("Show trails", 
						NSUserDefaults.StandardUserDefaults.BoolForKey("ShowTrails"))),
					(_boolFollowSelectedPlane = new BooleanElement ("Follow selected plane", 
						NSUserDefaults.StandardUserDefaults.BoolForKey("FollowPlane")))
				},
				new Section () {
					new StringElement(string.Format("Version {0} ({1})",
						NSBundle.MainBundle.InfoDictionary [new NSString ("CFBundleVersion")].ToString (),
						BUILD_DATE))
				}
			};

			_entryInterval.KeyboardType = UIKeyboardType.NumberPad;
			_entryInterval.AutocorrectionType = UITextAutocorrectionType.No;
			_entryInterval.AutocapitalizationType = UITextAutocapitalizationType.None;

			_entryUrl.KeyboardType = UIKeyboardType.Url;
			_entryUrl.AutocorrectionType = UITextAutocorrectionType.No;
			_entryUrl.AutocapitalizationType = UITextAutocapitalizationType.None;
		}

		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);

			if (_entryUrl.Value != null)
				NSUserDefaults.StandardUserDefaults.SetString(_entryUrl.Value, "Url"); 
			if (_entryInterval.Value != null)
				NSUserDefaults.StandardUserDefaults.SetString(_entryInterval.Value, "Interval"); 

			NSUserDefaults.StandardUserDefaults.SetBool(_boolFaa.Value, "Faa"); 

			NSUserDefaults.StandardUserDefaults.SetBool(_boolShowAirports.Value, "ShowAirports"); 
			NSUserDefaults.StandardUserDefaults.SetBool(_boolShowTrails.Value, "ShowTrails");
			NSUserDefaults.StandardUserDefaults.SetBool(_boolFollowSelectedPlane.Value, "FollowPlane");
		}

		private void ShowCurrentLocation (object sender, EventArgs e)
		{
			_mapView.ShowsUserLocation = _mapView.ShowsUserLocation ? false : true;
		}

		private void DownloadRoutesDb ()
		{
			var webClient = new WebClient();

			webClient.DownloadProgressChanged += (s, e) => {
				InvokeOnMainThread(delegate {
					_stringDownload.GetActiveCell().TextLabel.Text = string.Format("Downloading... ({0}%)", e.ProgressPercentage);
					_stringDownload.GetActiveCell().SetNeedsLayout();
				});
			};

			webClient.DownloadDataCompleted += (s, e) => {
				if (e.Result == null)
				{
					InvokeOnMainThread(delegate {
						new UIAlertView ("Error", "File could not be downloaded", null, "OK", null).Show();
					});
				}
				else
				{
					try
					{
						var bytes = e.Result; // get the downloaded data

						FileStream fstream = new FileStream(localPathUnzg, FileMode.Create);
						MemoryStream stream = new MemoryStream(bytes);
						GZipStream uncompressed = new GZipStream(stream, CompressionMode.Decompress);
						uncompressed.CopyTo(fstream);

						uncompressed.Flush();
						uncompressed.Close();

						stream.Dispose();
						fstream.Dispose();
					}
					catch
					{
						InvokeOnMainThread(delegate {
							new UIAlertView ("Error", "File could not be downloaded", null, "OK", null).Show();
						});
					}

					InvokeOnMainThread(delegate {
						new UIAlertView ("Done", "File downloaded and saved", null, "OK", null).Show();
					});
				}

				InvokeOnMainThread(delegate {
					string fileDownloaded = string.Empty;
					if (File.Exists (localPathUnzg)) {
						FileInfo fi = new FileInfo (localPathUnzg);
						fileDownloaded = string.Format (" ({0})", fi.CreationTime);
					}

					_stringDownload.GetActiveCell().TextLabel.Text = "Download" + fileDownloaded;
					_stringDownload.GetActiveCell().SetNeedsLayout();
				});
			};

			var url = new Uri(@"http://www.virtualradarserver.co.uk/Files/StandingData.sqb.gz");
			webClient.DownloadDataAsync(url);
		}
	}
}
