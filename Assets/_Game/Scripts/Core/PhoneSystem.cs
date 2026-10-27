using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>Sam's phone: texts that arrive during the night, plus the memories you find.</summary>
    public class PhoneSystem : MonoBehaviour
    {
        public class Entry
        {
            public string sender, text, time;
            public bool creepy, unread;
        }

        public class Snapshot
        {
            public List<int> sent;
            public List<Entry> inbox;
        }

        public readonly List<Entry> Inbox = new List<Entry>();
        readonly HashSet<int> sent = new HashSet<int>();
        List<Message> messages = new List<Message>();
        NightPlan plan;

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var e in Inbox) if (e.unread) n++;
                return n;
            }
        }

        public void Begin(List<Message> nightMessages, NightPlan nightPlan)
        {
            StopAllCoroutines();
            messages = nightMessages;
            plan = nightPlan;
            Inbox.Clear();
            sent.Clear();
            Refresh();
        }

        public void Tick(float minute)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                if (sent.Contains(i) || minute < m.minute) continue;
                sent.Add(i);
                if (m.when != null && !m.when(plan)) continue;
                Deliver(m.sender, m.text, m.creepy);
                return; // one per frame so the buzzes don't stack
            }
        }

        public void Deliver(string sender, string text, bool creepy)
        {
            Inbox.Add(new Entry { sender = sender, text = text, creepy = creepy, unread = true, time = GameDirector.FormatTime(GameDirector.Instance.Minute) });
            if (AudioManager.Instance != null) AudioManager.Instance.PhoneBuzz();
            if (UIManager.Instance != null)
            {
                string preview = text.Replace("\n", " ");
                if (preview.Length > 60) preview = preview.Substring(0, 57) + "...";
                UIManager.Instance.ShowToast(sender, preview, creepy, true);
            }
            Refresh();
        }

        public void DeliverLater(string sender, string text, bool creepy, float delay)
        {
            StartCoroutine(Later(sender, text, creepy, delay));
        }

        IEnumerator Later(string sender, string text, bool creepy, float delay)
        {
            yield return new WaitForSeconds(delay);
            Deliver(sender, text, creepy);
        }

        public void MarkAllRead()
        {
            foreach (var e in Inbox) e.unread = false;
            Refresh();
        }

        void Refresh()
        {
            if (UIManager.Instance != null) UIManager.Instance.RefreshPhone(this);
        }

        public Snapshot Capture()
        {
            var s = new Snapshot { sent = new List<int>(sent), inbox = new List<Entry>() };
            foreach (var e in Inbox) s.inbox.Add(new Entry { sender = e.sender, text = e.text, time = e.time, creepy = e.creepy, unread = e.unread });
            return s;
        }

        public void Restore(Snapshot s)
        {
            StopAllCoroutines();
            sent.Clear();
            foreach (var i in s.sent) sent.Add(i);
            Inbox.Clear();
            foreach (var e in s.inbox) Inbox.Add(new Entry { sender = e.sender, text = e.text, time = e.time, creepy = e.creepy, unread = e.unread });
            Refresh();
        }
    }
}
