using System;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace PolandVisaParser {
	internal class Scenario {

		private bool scenarioCompleted = false;

		public void Screen_1(IWebDriver webDriver, InputData inputData, string city) {
			webDriver.SwitchTo().Frame(
				webDriver.FindElement(
					By.CssSelector( "iframe[src^=\"https://polandonline.vfsglobal.com/poland-ukraine-appointment\"]" )
				)
			);
			//pushing on find date link (screen 1)
			webDriver.FindElement( By.Id( "ctl00_plhMain_lnkSchApp" ) ).Click();
		}
		public void Screen_2( IWebDriver webDriver, InputData inputData, string city ) {
			SelectElement selectOffice = new SelectElement( webDriver.FindElement( By.Id( "ctl00_plhMain_cboVAC" ) ) );
			string officeOptionText = selectOffice.Options.First( x => x.Text.Contains( city ) ).Text;
			selectOffice.SelectByText( officeOptionText );
			//selecting type(screen 2)
			SelectElement selectType = new SelectElement( webDriver.FindElement( By.Id( "ctl00_plhMain_cboPurpose" ) ) );
			selectType.SelectByValue( "1" );
			//submiting form (screen 2)
			webDriver.FindElement( By.Id( "ctl00_plhMain_btnSubmit" ) ).Click();
		}

		public void Screen_3( IWebDriver webDriver, InputData inputData, string city ) {
			//writting count of aplicants (screen 3)
			webDriver.FindElement( By.Id( "ctl00_plhMain_tbxNumOfApplicants" ) ).Clear();
			webDriver.FindElement( By.Id( "ctl00_plhMain_tbxNumOfApplicants" ) ).SendKeys( inputData.PeopleCount.ToString() );

			//selecting visa type (screen 3)
			SelectElement selectVisaType = new SelectElement( webDriver.FindElement( By.Id( "ctl00_plhMain_cboVisaCategory" ) ) );
			string visaTypeText = selectVisaType.Options.First( x => x.Text.Contains( inputData.VisaType ) ).Text;
			selectVisaType.SelectByText( visaTypeText );

			scenarioCompleted = true;
		}

		public void TryScenario( 
			Action<IWebDriver, InputData, string> scenarioStep,
			IWebDriver webDriver, 
			InputData inputData, 
			string city 
		) {
			try {
				if( scenarioCompleted ) {
					return;
				}
				scenarioStep( webDriver, inputData, city );
			} catch (Exception ex) {
				webDriver.Navigate().GoToUrl( inputData.ConsulatUrl );
				TryScenario( Screen_1, webDriver, inputData, city );
				TryScenario( Screen_2, webDriver, inputData, city );
				TryScenario( Screen_3, webDriver, inputData, city );
			}
		}

		public void RunScenario( 
			IWebDriver webDriver,
			InputData inputData,
			string city
		) {
			//Screen 1
			TryScenario( Screen_1, webDriver, inputData, city );

			//Screen 2
			TryScenario( Screen_2, webDriver, inputData, city );
			//Screen 3
			TryScenario( Screen_3, webDriver, inputData, city );
		}
	}
}
