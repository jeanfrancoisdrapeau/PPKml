using System;

using MapKit;
using CoreLocation;

namespace PPKml
{
	public class AirportAnnotation : MKAnnotation
	{
		public string Id;
		public override CLLocationCoordinate2D Coordinate {get;set;}
		string title;
		public override string Title { get{ return title; }}
		public AirportAnnotation (string id, CLLocationCoordinate2D coordinate, string title) {
			this.Id = id;
			this.Coordinate = coordinate;
			this.title = title;
		}
	}
}

