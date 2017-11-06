using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;

namespace WallWater
{
    [Serializable]
    public class UserSettings
    {
        private UserSettings()
        {

        }

        private double maxCalPressure = 1200;

        public double MaxCalPressure
        {
            get { return maxCalPressure; }
            set { maxCalPressure = value; }
        }

        private Stretch _stretch = Stretch.None;
        public Stretch Stretch
        {
            get { return _stretch; }
            set { _stretch = value; }

        }

        private string _imagePath;
        public string ImagePath
        {
            get { return _imagePath; }
            set { _imagePath = value; }
        }

        public void Flush()
        {
            string fName = "settings.conf";
            FileStream fs = null;
            fs = new FileStream(fName, FileMode.Create);
            XmlSerializer xmlSerial = new XmlSerializer(typeof(UserSettings));
            xmlSerial.Serialize(fs, this);
            fs.Dispose();
        }

        public static UserSettings GetInstance()
        {
            string fName = "settings.conf";
            UserSettings res = null;
            try
            {
                if (File.Exists(fName))
                {
                    FileStream fs = new FileStream(fName, FileMode.Open);
                    XmlSerializer xmlSerial = new XmlSerializer(typeof(UserSettings));
                    res = xmlSerial.Deserialize(fs) as UserSettings;
                    fs.Dispose();
                }

            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }

            if (res == null)
            {
                res = new UserSettings();
            }

            return res;
        }

    }
}
