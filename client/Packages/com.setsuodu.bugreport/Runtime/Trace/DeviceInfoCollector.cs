using Setsuodu.BugReport.Models;
using UnityEngine;

namespace Setsuodu.BugReport.Trace
{
    public static class DeviceInfoCollector
    {
        public static DeviceInfo Collect()
        {
            return new DeviceInfo
            {
                platform = Application.platform.ToString(),
                osVersion = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                deviceId = SystemInfo.deviceUniqueIdentifier
            };
        }
    }
}
