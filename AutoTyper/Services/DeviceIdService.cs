using System;
using System.IO;

namespace AutoTyper.Services
{
    public class DeviceIdService
    {
        private const string DeviceIdFileName = "device_id.txt";
        
        public string GetDeviceId()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoTyper");
            string filePath = Path.Combine(appDataPath, DeviceIdFileName);

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            if (File.Exists(filePath))
            {
                string id = File.ReadAllText(filePath).Trim();
                if (Guid.TryParse(id, out _))
                {
                    return id;
                }
            }

            string newId = Guid.NewGuid().ToString();
            File.WriteAllText(filePath, newId);
            return newId;
        }
    }
}
