using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Game
{
    private void SetupNetWeatherHostTest()
    {
        Step(1, () =>
        {
            World.Rules.DoMobSpawning = false;
            Cmds.Execute(Player, "/weather rain 600");
            NetTestSignal("weather-rain");
        });
        WaitUntil(() => File.Exists(NetTestFile("weather-rain-seen")) && World.RainStrength == 1);
        Step(15, () =>
        {
            Shot("weather_host_rain");
            var guest = Server.Clients.Single(c => c.P != null).P;
            var bolt = new LightningBolt(World) { RemainingFlashes = 3 };
            bolt.SetPositionAndPrev(guest.X + 6, 4, guest.Z + 6); World.AddEntityNow(bolt);
            NetTestSignal("weather-bolt");
        });
        WaitUntil(() => File.Exists(NetTestFile("weather-bolt-seen")));
        Step(1, () => { Cmds.Execute(Player, "/weather clear 600"); NetTestSignal("weather-clear"); });
        WaitUntil(() => File.Exists(NetTestFile("weather-clear-seen")) && World.RainStrength == 0);
        Step(10, () => Shot("weather_host_clear"));
    }

    private void SetupNetWeatherJoinTest()
    {
        WaitUntil(() => File.Exists(NetTestFile("weather-rain")) && World.RainStrength == 1 && Renderer.RainColumns > 0);
        Step(10, () =>
        {
            if (Sound.RainVoices > 1) throw new InvalidOperationException("Nawarstwiony deszcz u gościa");
            Log.Info($"NET WEATHER rain: strength={World.RainStrength} columns={Renderer.RainColumns} voices={Sound.RainVoices} native={Renderer.WeatherNativeTextures}");
            Shot("weather_guest_rain"); NetTestSignal("weather-rain-seen");
        });
        WaitUntil(() => File.Exists(NetTestFile("weather-bolt")) && World.Entities.OfType<LightningBolt>().Any());
        Step(1, () => { Log.Info("NET WEATHER lightning: OK"); NetTestSignal("weather-bolt-seen"); });
        WaitUntil(() => File.Exists(NetTestFile("weather-clear")) && World.RainStrength == 0 && Renderer.RainColumns == 0 && Sound.RainVoices == 0);
        Step(10, () => { Shot("weather_guest_clear"); NetTestSignal("weather-clear-seen"); Log.Info("NET WEATHER clear: OK"); });
    }
}
