using System.Collections.Generic;
using OpenQA.Selenium.Chrome;

namespace PolandVisaParser {
	public class ChromeOptionsWithPrefs : ChromeOptions {
		public Dictionary<string, object> prefs { get; set; }
	}
}
