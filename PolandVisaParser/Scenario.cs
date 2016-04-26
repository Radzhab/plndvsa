using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;

namespace PolandVisaParser {
	internal class Scenario {

		private bool m_scenarioCompleted = false;

		private const string m_firstLevelFrame = "iframe[src^=\"https://polandonline.vfsglobal.com/poland-ukraine-appointment\"]";
		private const string m_secondLevelFrameCaptchaAnchor = "iframe[src^=\"https://www.google.com/recaptcha/api2/anchor\"]";
		private const string m_secondLevelFrameCaptchaFrame = "iframe[src^=\"https://www.google.com/recaptcha/api2/frame\"]";
		private const string m_getElementLocationJavascript = "return arguments[0].getBoundingClientRect()";
		private const string m_yandexTranslatorApiKey ="trnsl.1.1.20160426T193513Z.05971e32dddb968e.d76225777061c14376452bb7944a5b70a5105da2";


		public void Screen_1(IWebDriver webDriver, InputData inputData, string city) {
			webDriver.SwitchTo().Frame(webDriver.FindElement(By.CssSelector( m_firstLevelFrame )));
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

			IWebElement captchaFrame = webDriver.FindElement( By.CssSelector( m_secondLevelFrameCaptchaAnchor ) );
			webDriver.SwitchTo().Frame( captchaFrame ); // 2 frame level anchor

			//push on captcha checkbox
			webDriver.FindElement( By.Id( "recaptcha-anchor" ) ).Click();
			webDriver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(5));
			Thread.Sleep(2000);
			//get images elements
			webDriver.SwitchTo().ParentFrame(); // 1 frame level
			webDriver.SwitchTo().ParentFrame(); // 0 frame level (native html)

			IWebElement firstFrame = webDriver.FindElement( By.CssSelector( m_firstLevelFrame ) );
			Point centerDivLocationPoint = new Point(firstFrame.Location.X, firstFrame.Location.Y);

			webDriver.SwitchTo().Frame( firstFrame ); // back to 1 frame level

			captchaFrame = webDriver.FindElement( By.CssSelector( m_secondLevelFrameCaptchaFrame ) );
			webDriver.SwitchTo().Frame( captchaFrame ); // 2 frame level frame

			IWebElement picturesSet = webDriver.FindElement( By.Id( "rc-imageselect-target" ) );

			Dictionary<string, object> getPicturesLocation = (Dictionary<string, object>)( (IJavaScriptExecutor)webDriver).ExecuteScript(
				m_getElementLocationJavascript, 
				picturesSet 
			);

			Rectangle picturesStateRectangle = new Rectangle(
				(int)double.Parse( getPicturesLocation["left"].ToString() ),
				(int)double.Parse( getPicturesLocation["top"].ToString() ) +15,
				(int)double.Parse( getPicturesLocation["width"].ToString() ) + 50,
				(int)double.Parse( getPicturesLocation["height"].ToString() ) + 50
			);

			picturesStateRectangle.X += centerDivLocationPoint.X + 60;

			Byte[] webPageScreenshot = webDriver.TakeScreenshot().AsByteArray;
			using( Stream memoryStream = new MemoryStream( webPageScreenshot ) )
			{
				Bitmap webPageScreenshotBitmap = new Bitmap( memoryStream );

				webPageScreenshotBitmap = webPageScreenshotBitmap.Clone(
					picturesStateRectangle,
					webPageScreenshotBitmap.PixelFormat
				);
				webPageScreenshotBitmap.Save( "pictures.jpg", ImageFormat.Jpeg );
			}

			StringBuilder yandexTranslatorUrlBuilder = new StringBuilder( @"https://translate.yandex.net/api/v1.5/tr.json/translate?key=" );
			yandexTranslatorUrlBuilder.Append(m_yandexTranslatorApiKey);
			yandexTranslatorUrlBuilder.Append( "&lang=uk-en" );
			yandexTranslatorUrlBuilder.Append( "&format=plain" );
			yandexTranslatorUrlBuilder.Append( "&text=" );
			yandexTranslatorUrlBuilder.Append( webDriver.FindElement( By.ClassName( "rc-imageselect-desc-no-canonical" ) ).Text );
			//
			WebRequest yandexApiRequest = WebRequest.Create( yandexTranslatorUrlBuilder.ToString());

			using (WebResponse yandexApiResponse = yandexApiRequest.GetResponse())
			{
				using (Stream data = yandexApiResponse.GetResponseStream())
				{
					using( var reader = new StreamReader( data ) ) {
						dynamic myResponse = JsonConvert.DeserializeObject<dynamic>( reader.ReadToEnd() );
						string translatedText = myResponse.text[0];
					}
				}
			}

			m_scenarioCompleted = true;
		}

		public void TryScenario( 
			Action<IWebDriver, InputData, string> scenarioStep,
			IWebDriver webDriver, 
			InputData inputData, 
			string city 
		) {
			try {
				if( m_scenarioCompleted ) {
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
