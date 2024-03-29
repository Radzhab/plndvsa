﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using PolandVisaParser.Data;

namespace PolandVisaParser
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			if (args == null || (args.Length == 0 || !File.Exists(args[0]) ) )
			{
				Console.WriteLine("File name is absent or incorrect! Please check corectness of file name/path and try again");
				Console.WriteLine("To continue, press any key...");
				Console.ReadKey();
				return;
			}

			ICollection<IWebDriver> webDriversList = new List<IWebDriver>();
			try
			{
				string inputParameters = File.ReadAllText(args[0]);
				InputData inputData = JsonConvert.DeserializeObject<InputData>( inputParameters );
				foreach (string city in inputData.Cities)
				{
					ChromeOptions chromeOptions = new ChromeOptions();
					chromeOptions.AddArgument( "--lang=en" );
					chromeOptions.AddArgument("--start-maximized");

					chromeOptions.AddUserProfilePreference( "intl.accept_languages", "en-US,en" );
					IWebDriver webDriver = new ChromeDriver( chromeOptions );
					webDriversList.Add(webDriver);
					DataForSearch dataForSearch = new DataForSearch()
					{
						City =  city,
						ConsulatUrl = inputData.ConsulatUrl,
						PeopleCount = inputData.PeopleCount.ToString(),
						VisaType = inputData.VisaType
					};

					Scenario scenario = new Scenario( webDriver, dataForSearch );

					//Initial goto url
					webDriver.Navigate().GoToUrl( inputData.ConsulatUrl );

					//Run scenario
					scenario.RunScenario();
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