// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public unsafe static class WeatherStruct
    {
        public static DateTime NextChange = new DateTime();
        public static Weather CurrentWeatherBase = new Weather(true);
        public static uint Intensity
        {
            get { return CurrentWeatherBase.Intensity; }
            set { CurrentWeatherBase.Intensity = value; }
        }
        public static uint Appearence
        {
            get { return CurrentWeatherBase.Appearence; }
            set { CurrentWeatherBase.Appearence = value; }
        }
        public static uint Direction
        {
            get { return CurrentWeatherBase.Direction; }
            set { CurrentWeatherBase.Direction = value; }
        }
        public static uint CurrentWeather
        {
            get { return CurrentWeatherBase.WeatherType; }
            set
            {
                CurrentWeatherBase.WeatherType = value;
                foreach (Client.GameState client in Server.GamePool)
                {
                    CurrentWeatherBase.Send(client);
                }
            }
        }
    }
}