namespace BlockCraft.Net;

/// <summary>Tryb --server. Gra wieloosobowa działa bez serwera dedykowanego: host otwiera świat w sieci z menu pauzy.</summary>
public static class DedicatedServer
{
    public static int Run(string[] args)
    {
        Console.WriteLine("BlockCraft nie potrzebuje serwera dedykowanego.");
        Console.WriteLine("Uruchom grę, wejdź do świata i wybierz w menu pauzy \"Otwórz w sieci\" - dostaniesz kod zaproszenia.");
        return 0;
    }
}
