using System.Configuration;

namespace PretestPosttestLib_CSharp
{

public class AppSetting
{
	public static bool Set(string appKey, string appValue)
	{
		Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
		configuration.AppSettings.Settings.Remove(appKey);
		configuration.AppSettings.Settings.Add(appKey, appValue);
		configuration.Save(ConfigurationSaveMode.Modified);
		ConfigurationManager.RefreshSection("appSettings");
		return true;
	}

	public static string Get(string appKey)
	{
		string result = "";
		try
		{
			if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings[appKey]))
			{
				result = ConfigurationManager.AppSettings[appKey];
			}
		}
		catch
		{
		}
		return result;
	}
}


}
