namespace Monapi.Worker.NewRelic;

/// <summary>
/// This class exists only for prototyping. It creates fake NewRelic data and write it 
/// to the database using NewRelicConnector.WriteToDatabase(). This class can be safely
/// deleted once a fully fleged simulator is implemented to test NewRelicConnector.GetApps().
/// </summary>
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
/// <summary>
/// Populate List with fake applications.
/// </summary>
    private void BuildList()
    {
        apps.Add(new NewRelicApp
        {
            AppId = "aabc123",
            AppName = "EC2-WEB-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "adef456",
            AppName = "EC2-WEB-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "aghi789",
            AppName = "LOAD-BAL",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ajkl012",
            AppName = "EC2-CDN-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "amno345",
            AppName = "ZTA-APP",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "apqr678",
            AppName = "SQL-PROD-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "astu901",
            AppName = "SQL-PROD-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "avwx234",
            AppName = "SQL-TEST-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ayza567",
            AppName = "SQL-TEST-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "abcd890",
            AppName = "API-CONT",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "babc123",
            AppName = "DOCK-RUN-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bdef456",
            AppName = "DOCK-RUN-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bghi789",
            AppName = "POS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bjkl012",
            AppName = "POS-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bmno345",
            AppName = "POS-TEST",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bpqr678",
            AppName = "PSA-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bstu901",
            AppName = "IS-DC1",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bvwx234",
            AppName = "IS-DC2",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "byza567",
            AppName = "IS-DC3",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "bbcd890",
            AppName = "WAN-UP-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cabc123",
            AppName = "WAN-UP-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cdef456",
            AppName = "WAN-UP-03",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cghi789",
            AppName = "NFS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cjkl012",
            AppName = "iSCSI-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cmno345",
            AppName = "DNS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cpqr678",
            AppName = "DNS-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cstu901",
            AppName = "MONARCH",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cvwx234",
            AppName = "HV1",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cyza567",
            AppName = "HV2",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "cbcd890",
            AppName = "HV3",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dabc123",
            AppName = "HV4",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ddef456",
            AppName = "HV-LAB",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dghi789",
            AppName = "CRM-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "djkl012",
            AppName = "CRM-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dmno345",
            AppName = "ATERA-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dpqr678",
            AppName = "VPN-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dstu901",
            AppName = "VPN-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dvwx234",
            AppName = "WSUS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dyza567",
            AppName = "WSUS-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "dbcd890",
            AppName = "WINS-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "eabc123",
            AppName = "WINS-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "edef456",
            AppName = "EDR-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "eghi789",
            AppName = "EDR-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ejkl012",
            AppName = "EDR-TEST",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "emno345",
            AppName = "RMM-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "epqr678",
            AppName = "RMM-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "estu901",
            AppName = "MDM-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "evwx234",
            AppName = "MDM-02",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "eyza567",
            AppName = "RSNAP-01",
            Status = PickStatus()
        });
        apps.Add(new NewRelicApp
        {
            AppId = "ebcd890",
            AppName = "RSNAP-02",
            Status = PickStatus()
        });
    }

/// <summary>
/// Given a list of applications, modify the status property at random.
/// </summary>
    private void UpdateList()
    {
        foreach (NewRelicApp app in apps)
        {
            app.Status = PickStatus();
        }
    }

/// <summary>
/// Pick a random status. Selection is weighted to return Operational most of the time.
/// </summary>
/// <returns>Random application status.</returns>
    private String PickStatus()
    {
        int roll = (new Random()).Next(100);
        if (roll < 95)
        {
            return "Operational";
        }
        if (roll < 98)
        {
            return "Degraded";
        }
        return "Down";
    }

}