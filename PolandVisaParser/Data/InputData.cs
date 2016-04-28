using System.Collections.Generic;

namespace PolandVisaParser.Data {
	internal sealed class InputData {
		public string ConsulatUrl { get; set; }
		public string VisaType { get; set; }
		public IEnumerable<string> Cities { get; set; }
		public int PeopleCount { get; set; }
	}
}
