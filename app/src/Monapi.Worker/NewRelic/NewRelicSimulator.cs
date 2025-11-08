namespace Monapi.Worker.NewRelic;

public class NewRelicSimulator
{
    private NewRelicConnector nrc;
    private List<NewRelicApp> apps = new List<NewRelicApp>();
    public NewRelicSimulator()
    {
        nrc = new NewRelicConnector();
        BuildList();
    }

    public async void RunLoop()
    {
        UpdateList();
        await nrc.WriteToDatabase(apps);
    }

    private void BuildList()
    {
        apps.Add(new NewRelicApp
        {
            AppId = "abc123",
            AppName = "HV1",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "def456",
            AppName = "MONARCH",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ghi789",
            AppName = "IS-DC1",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "jkl012",
            AppName = "IS-DC2",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "mno345",
            AppName = "IS-DC3",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "pqr678",
            AppName = "OPCS",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "stu901",
            AppName = "RMM-HOST",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "vwx234",
            AppName = "DNS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "yza567",
            AppName = "DNS-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bcd890",
            AppName = "API-GATEWAY",
            Status = PickStatus()
        });
    }

    private void UpdateList()
    {
        foreach (NewRelicApp app in apps)
        {
            app.Status = PickStatus();
        }
    }
    
    private String PickStatus()
    {
        int roll = (new Random()).Next(100);
        if (roll < 70)
        {
            return "Operational";
        }
        if (roll < 90)
        {
            return "Degraded";
        }
        return "Down";
    }

}