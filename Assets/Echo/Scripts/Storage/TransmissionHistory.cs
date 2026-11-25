using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Echo {
[Serializable] public class EchoMessage { public string text, time; public bool incoming; }
[Serializable] class EchoMessageList { public List<EchoMessage> items = new List<EchoMessage>(); }
public sealed class TransmissionHistory {
    readonly string path;
    readonly EchoMessageList data = new EchoMessageList();
    public IList<EchoMessage> Items => data.items;
    public TransmissionHistory() : this(Path.Combine(Application.persistentDataPath, "echo-history.json")) { }
    public TransmissionHistory(string filePath) { path = filePath; try { if (File.Exists(path)) data = JsonUtility.FromJson<EchoMessageList>(File.ReadAllText(path)) ?? new EchoMessageList(); } catch (Exception e) { Debug.LogWarning("History load: " + e.Message); data = new EchoMessageList(); } }
    public void Add(string text, bool incoming) { data.items.Add(new EchoMessage { text = text, incoming = incoming, time = DateTime.Now.ToString("HH:mm:ss") }); if (data.items.Count > 200) data.items.RemoveAt(0); Save(); }
    public void Clear() { data.items.Clear(); Save(); }
    void Save() { try { File.WriteAllText(path, JsonUtility.ToJson(data)); } catch (Exception e) { Debug.LogWarning("History save: " + e.Message); } }
}
}
