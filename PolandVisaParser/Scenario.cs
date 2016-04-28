using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using PolandVisaParser.Data;

namespace PolandVisaParser
{
	internal class Scenario
	{
		private bool m_scenarioCompleted;
		private readonly IWebDriver m_webDriver;
		private readonly DataForSearch m_dataForSearch;

		private const string m_firstLevelFrame = "iframe[src^=\"https://polandonline.vfsglobal.com/poland-ukraine-appointment\"]";
		private const string m_secondLevelFrameCaptchaAnchor = "iframe[src^=\"https://www.google.com/recaptcha/api2/anchor\"]";
		private const string m_secondLevelFrameCaptchaFrame = "iframe[src^=\"https://www.google.com/recaptcha/api2/frame\"]";
		private const string m_yandexTranslatorApiKey = "trnsl.1.1.20160426T193513Z.05971e32dddb968e.d76225777061c14376452bb7944a5b70a5105da2";
		private const string m_2captchaApiKey = "d42d830a2b7b1aa751f226c454c4cb55";

		public Scenario(
			IWebDriver mWebDriver, 
			DataForSearch mDataForSearch
		){
			m_webDriver = mWebDriver;
			m_dataForSearch = mDataForSearch;
		}

		public void RunScenario() {
			//Screen 1
			TryScenario( Screen_1 );
			//Screen 2
			TryScenario( Screen_2 );
			//Screen 3
			TryScenario( Screen_3 );
		}

	#region Screens in pipeline
		private void Screen_1(){

			m_webDriver.Navigate().GoToUrl( m_dataForSearch.ConsulatUrl );

			m_webDriver.SwitchTo().Frame(
				m_webDriver.FindElement(By.CssSelector(m_firstLevelFrame))
			);
			
			//pushing on find date link (screen 1)
			m_webDriver.FindElement(By.Id("ctl00_plhMain_lnkSchApp")).Click();
		}

		private void Screen_2(){
			SelectElement selectOffice = new SelectElement(
				m_webDriver.FindElement(By.Id("ctl00_plhMain_cboVAC"))
			);
			string officeOptionText = selectOffice.Options
				.First(x => x.Text.Contains(m_dataForSearch.City))
				.Text;
			selectOffice.SelectByText(officeOptionText);

			//selecting type(screen 2)
			SelectElement selectType = new SelectElement(
				m_webDriver.FindElement(By.Id("ctl00_plhMain_cboPurpose"))
			);
			selectType.SelectByValue("1");

			//submiting form (screen 2)
			m_webDriver.FindElement(By.Id("ctl00_plhMain_btnSubmit")).Click();
		}

		private void Screen_3(){
			//solving captcha
			SolveCaptcha();

			//writting count of aplicants (screen 3)
			m_webDriver.FindElement(
				By.Id("ctl00_plhMain_tbxNumOfApplicants")
			)
			.Clear();

			m_webDriver.FindElement
				(By.Id("ctl00_plhMain_tbxNumOfApplicants")
			)
			.SendKeys(m_dataForSearch.PeopleCount);

			//selecting visa type (screen 3)
			SelectElement selectVisaType = new SelectElement(
				m_webDriver.FindElement(By.Id("ctl00_plhMain_cboVisaCategory"))
			);
			string visaTypeText = selectVisaType.Options
				.First(x => x.Text.Contains(m_dataForSearch.VisaType))
				.Text;
			selectVisaType.SelectByText(visaTypeText);

			m_scenarioCompleted = true;
		}
	#endregion

	#region private steps methods
		private void PushOnButtonCaptcha(){
			IWebElement captchaFrame = m_webDriver.FindElement(
				By.CssSelector(m_secondLevelFrameCaptchaAnchor)
			);
			m_webDriver.SwitchTo().Frame(captchaFrame); // 2 frame level anchor

			//push on captcha checkbox
			m_webDriver.FindElement(By.Id("recaptcha-anchor")).Click();
			m_webDriver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(5));
			Thread.Sleep(2000);
			//get images elements
			m_webDriver.SwitchTo().ParentFrame(); // 1 frame level
		}

		private void SolvePicturesCaptcha()
		{
			IWebElement captchaFrame = m_webDriver.FindElement(
				By.CssSelector(m_secondLevelFrameCaptchaFrame)
			);
			m_webDriver.SwitchTo().Frame(captchaFrame); // 2 frame level frame

			byte[] image;
			int imageDimension;
			try
			{
				IWebElement captchaPicture = m_webDriver.FindElement(
					By.CssSelector("img[class^=\"rc-image-tile\"]")
				);
				imageDimension = int.Parse(
					captchaPicture.GetAttribute("class")
					.Last()
					.ToString()
				);
				using (WebClient webClient = new WebClient())
				{
					image = webClient.DownloadData(captchaPicture.GetAttribute("src"));
				}
			}
			catch (NoSuchElementException)
			{
				m_webDriver.SwitchTo().ParentFrame();
				return;
			}

			//translating description from ukrainian to english
			string textToTranslate = m_webDriver.FindElement(
				By.ClassName("rc-imageselect-desc-no-canonical")
			).Text;

			string translatedText = TranslateCaptchaDescription(
				textToTranslate,
				m_yandexTranslatorApiKey
			);

			//sending request for solving captcha 
			IEnumerable<int> captchaSolutionList;
			bool isCaptchaSolved = GetCaptchaInstuctionsFromService(
				image, 
				translatedText, 
				imageDimension.ToString(),
				out captchaSolutionList
			);
			if (!isCaptchaSolved)
			{
				throw new NotFoundException("Captcha haven't been solved");
			}

			//marking solved pictures in captcha
			IList<IWebElement> pictures = m_webDriver.FindElements(
				By.ClassName("rc-image-tile-target")
			);

			foreach (int resCaptcha in captchaSolutionList)
			{
				int indexOfCaptchElement = resCaptcha;
				pictures[indexOfCaptchElement - 1].Click();
			}

			//pressing submit button on captcha frame
			m_webDriver.FindElement(
				By.Id("recaptcha-verify-button")
			).Click();

			m_webDriver.SwitchTo().ParentFrame();
		}

		private bool GetCaptchaInstuctionsFromService(
			byte[] image, 
			string description, 
			string gridDimension,
			out IEnumerable<int> insturctions
		){
			insturctions = null;
			try
			{
				using (WebClient client = new WebClient())
				{
					NameValueCollection captchaServiceRequestBody = new NameValueCollection(){
						{"body", Convert.ToBase64String(image)},
						{"key", m_2captchaApiKey},
						{"method", "base64"},
						{"recaptcha", "1"},
						{"textinstructions", description},
						{"recaptchacols", gridDimension},
						{"recaptcharows", gridDimension}
					};

					//sending captcha picture with description for solving
					byte[] response = client.UploadValues( 
						@"http://2captcha.com/in.php", 
						captchaServiceRequestBody 
					);
					string result = Encoding.UTF8.GetString(response);

					if (result.StartsWith("OK|"))
					{
						string captchaId = result.Substring(3);

						using (HttpClient httpClient = new HttpClient())
						{
							StringBuilder stringBuilder = new StringBuilder(@"http://2captcha.com/res.php?key=");
							stringBuilder.Append(m_2captchaApiKey);
							stringBuilder.Append("&action=get");
							stringBuilder.Append("&id=");
							stringBuilder.Append(captchaId);
							
							//waiting 5 seconds for solving captcha service
							Thread.Sleep(TimeSpan.FromSeconds(5));
							
							//sending request to captcha solving service to get solution
							using (HttpResponseMessage responseMessageResolvedCaptcha = httpClient.GetAsync(stringBuilder.ToString()).Result)
							{
								using (HttpContent content = responseMessageResolvedCaptcha.Content)
								{
									result = content.ReadAsStringAsync().Result;
									if (result.StartsWith("OK|click"))
									{
										insturctions = result.Substring(9)
											.Split('/')
											.Select(int.Parse);
										return true;
									}
								}
							}
						}
					}
				}
			}
			catch
			{
				return false;
			}
			return false;
		}

		private void SolveCaptcha(){
			//checking on checkbox "I'm not a robot"
			PushOnButtonCaptcha();
			//solving pictures (if need)
			SolvePicturesCaptcha();
		}

		private string TranslateCaptchaDescription(
			string textToTranslate, 
			string translatorApiKey
		){
			string translatedText = string.Empty;
			StringBuilder yandexTranslatorUrlBuilder = new StringBuilder(@"https://translate.yandex.net/api/v1.5/tr.json/translate?key=");
			yandexTranslatorUrlBuilder.Append(translatorApiKey);
			yandexTranslatorUrlBuilder.Append("&lang=uk-en");
			yandexTranslatorUrlBuilder.Append("&format=plain");
			yandexTranslatorUrlBuilder.Append("&text=");
			yandexTranslatorUrlBuilder.Append(textToTranslate);

			WebRequest yandexApiRequest = WebRequest.Create(yandexTranslatorUrlBuilder.ToString());
			try
			{
				//sending request to translation service to get translation from ukraine to english
				using (WebResponse yandexApiResponse = yandexApiRequest.GetResponse())
				{
					using (Stream data = yandexApiResponse.GetResponseStream())
					{
						using (var reader = new StreamReader(data))
						{
							dynamic myResponse = JsonConvert.DeserializeObject<dynamic>(reader.ReadToEnd());
							translatedText = myResponse.text[0];
						}
					}
				}
			}
			catch
			{
				return null;
			}

			return translatedText;
		}

		private void TryScenario(Action scenarioStep)
		{
			try
			{
				if (m_scenarioCompleted)
				{
					return;
				}
				scenarioStep();
			}
			catch (Exception ex)
			{
				TryScenario(Screen_1);
				TryScenario(Screen_2);
				TryScenario(Screen_3);
			}
		}
	#endregion
	}
}