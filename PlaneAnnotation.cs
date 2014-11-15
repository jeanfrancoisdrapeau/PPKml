using System;
using System.Collections.Generic;

using CoreLocation;
using MapKit;

using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;

namespace PPKml
{
	public class PlaneAnnotation : MKAnnotation
	{
		string title, subtitle;
		public override string Title { get{ return title; }}
		public override string Subtitle { get{ return subtitle; }}

		public DateTime _LastSeen;

		public bool _NeedsRefresh = true;

		public PlaneAnnotation (bool isfaa,
			DateTime lastseen,
			Placemark placemark, 
			double mapheading, 
			double heading, 
			string flight, 
			double level, 
			string hex,
			string pcode,
			string ptype,
			double speed,
			double unixepochtime,
			CLLocationCoordinate2D originalcoord,
			RouteDetail routedetail)
		{
			_IsFaa = isfaa;

			_LastSeen = lastseen;
			_Placemark = placemark;

			_Hex = hex;

			_Heading = heading;
			_MapHeading = mapheading;
			_Level = level;
			_Flight = flight;
			_PCode = pcode;
			_PType = ptype;
			_Speed = speed;
			_ChangingElevation = ChangingElevation.Neutral;
			_ChangingSpeed = ChangingSpeed.Neutral;

			_RouteDetail = routedetail;

			_UnixEpochTime = unixepochtime;

			_OriginalCoord = originalcoord;

			title = string.Empty;
			subtitle = string.Empty;
		}

		public PlaneAnnotation(PlaneAnnotation oldPa)
		{
			_IsFaa = oldPa._IsFaa;

			_LastSeen = oldPa._LastSeen;
			_Placemark = oldPa._Placemark;

			_Hex = oldPa._Hex;

			_Heading = oldPa._Heading;
			_MapHeading = oldPa._MapHeading;
			_Level = oldPa._Level;
			_Flight = oldPa._Flight;
			_PCode = oldPa._PCode;
			_PType = oldPa._PType;
			_Speed = oldPa._Speed;
			_ChangingElevation = ChangingElevation.Neutral;
			_ChangingSpeed = ChangingSpeed.Neutral;

			_RouteDetail = oldPa._RouteDetail;

			_UnixEpochTime = oldPa._UnixEpochTime;

			_OriginalCoord = oldPa._OriginalCoord;

			coordinatesList = oldPa.coordinatesList;

			title = string.Empty;
			subtitle = string.Empty;
		}

		public bool _IsFaa = false;
		public Placemark _Placemark;
		public string _Hex;
		public double _Heading;
		public double _MapHeading;
		public string _Flight;
		public double _Level;
		public string _PCode;
		public string _PType;
		public string _OperatorIcao;
		public double _Speed;
		public RouteDetail _RouteDetail;
		public bool _SameCoord;
		public double _UnixEpochTime;
		public CLLocationCoordinate2D _OriginalCoord;

		public List<CLLocationCoordinate2D> coordinatesList = new List<CLLocationCoordinate2D>();
		public MKPolyline trail;

		public enum ChangingElevation
		{
			Neutral,
			Climbing,
			Descending
		}
		public ChangingElevation _ChangingElevation;

		public enum ChangingSpeed
		{
			Neutral,
			Accelerating,
			Decelerating
		}
		public ChangingSpeed _ChangingSpeed;

		public override CLLocationCoordinate2D Coordinate {
			get {
				SharpKml.Dom.Point point = (SharpKml.Dom.Point)_Placemark.Geometry;

				//Console.WriteLine (string.Format("Lat:{0} Long:{1}", point.Coordinate.Latitude, point.Coordinate.Longitude));

				return new CLLocationCoordinate2D (point.Coordinate.Latitude, 
					point.Coordinate.Longitude);
			}

			set {
				SharpKml.Dom.Point point = new Point ();
				point.Coordinate = new Vector (value.Latitude, value.Longitude);

				_Placemark.Geometry = point;
			}
		}
	}
}