using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace PolandVisaParser
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			if (args == null || (args.Length == 0 && !File.Exists(args[0]) ) )
			{
				Console.WriteLine("File name is absent or incorrect! Please check corectness of file name/path and try again");
				Console.WriteLine("To continue, press any key...");
				Console.ReadKey();
				return;
			}

			ICollection<IWebDriver> webDriversList = new List<IWebDriver>();
			try
			{
				//webDriver = new ChromeDriver();
				string inputParameters = File.ReadAllText(args[0]);
				InputData inputData = JsonConvert.DeserializeObject<InputData>( inputParameters );
				foreach (string city in inputData.Cities)
				{
					ChromeOptions chromeOptions = new ChromeOptions();
					chromeOptions.AddArgument( "--lang=en" );
					chromeOptions.AddArgument( "--start-maximized" );
					
					IWebDriver webDriver = new ChromeDriver( chromeOptions );
					webDriversList.Add(webDriver);
					
					//Initial goto url
					webDriver.Navigate().GoToUrl( inputData.ConsulatUrl );

					//Finding frame with links (screen 1)
					webDriver.SwitchTo().Frame(
						webDriver.FindElement(
							By.CssSelector("iframe[src^=\"https://polandonline.vfsglobal.com/poland-ukraine-appointment\"]")
						)
					);
					//pushing on find date link (screen 1)
					webDriver.FindElement(By.Id("ctl00_plhMain_lnkSchApp")).Click();
					
					//selecting office
					SelectElement selectOffice = new SelectElement(webDriver.FindElement(By.Id("ctl00_plhMain_cboVAC")));
					string officeOptionText = selectOffice.Options.First(x => x.Text.Contains(city)).Text;
					selectOffice.SelectByText( officeOptionText );
					//selecting office
					SelectElement selectType = new SelectElement( webDriver.FindElement( By.Id( "ctl00_plhMain_cboPurpose" ) ) );
					selectType.SelectByValue( "1" );
					//submiting form
					webDriver.FindElement( By.Id( "ctl00_plhMain_btnSubmit" ) )
						.Click();

					//writting count of aplicants
					webDriver.FindElement( By.Id( "ctl00_plhMain_tbxNumOfApplicants" ) ).Clear();
					webDriver.FindElement( By.Id( "ctl00_plhMain_tbxNumOfApplicants" ) ).SendKeys( inputData.PeopleCount.ToString());
						
					//selecting visa type
					SelectElement selectVisaType = new SelectElement( webDriver.FindElement( By.Id( "ctl00_plhMain_cboVisaCategory" ) ) );
					string visaTypeText = selectVisaType.Options.First( x => x.Text.Contains( inputData.VisaType ) ).Text;
					selectVisaType.SelectByText( visaTypeText );
					
					Console.WriteLine( "Parser running. For escape, press any button..." );
					Console.ReadKey();
				}
			}
			finally
			{
				foreach (IWebDriver webDriver in webDriversList)
				{
					webDriver.Quit();
				}
			}
		}
	}
}