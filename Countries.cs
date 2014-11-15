using System;
using System.IO;
using System.Collections.Generic;

using Foundation;

namespace PPKml
{
	public class Countries
	{
		public class CountryDetails
		{
			public string Code;
			public string Country;
		}

		public List<CountryDetails> countriesList = new List<CountryDetails>();

		public Countries ()
		{
			string path = Path.Combine(NSBundle.MainBundle.BundlePath, "Flags", "countries.txt");
			StreamReader sr = File.OpenText (path);
			while (!sr.EndOfStream) {
				string line = sr.ReadLine ();
				string[] lineSplit = line.Split (',');
				countriesList.Add(new CountryDetails() {
					Code = lineSplit[0],
					Country = lineSplit[1]
				});
			}
		}
	}
}

