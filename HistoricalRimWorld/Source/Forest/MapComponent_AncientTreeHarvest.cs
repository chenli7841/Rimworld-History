using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Persists partial ancient-tree work and guards one-time harvest resolution.</summary>
    public sealed class MapComponent_AncientTreeHarvest : MapComponent
    {
        private List<int> treeIds = new List<int>();
        private List<float> workProgress = new List<float>();
        private List<int> resolvedTreeIds = new List<int>();
        private readonly Dictionary<int, float> progressByTree = new Dictionary<int, float>();
        private readonly HashSet<int> resolvedIds = new HashSet<int>();

        public MapComponent_AncientTreeHarvest(Map map) : base(map) { }

        public float GetWorkProgress(int treeId) => progressByTree.TryGetValue(treeId, out float value) ? value : 0f;

        public float AddWork(int treeId, float amount)
        {
            if (treeId <= 0 || amount <= 0f) return GetWorkProgress(treeId);
            float value = GetWorkProgress(treeId) + amount;
            progressByTree[treeId] = value;
            return value;
        }

        public void ClearWorkProgress(int treeId) => progressByTree.Remove(treeId);

        public bool TryMarkResolved(int treeId)
        {
            return treeId > 0 && resolvedIds.Add(treeId);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                treeIds = new List<int>(progressByTree.Keys);
                workProgress = new List<float>();
                foreach (int id in treeIds) workProgress.Add(progressByTree[id]);
                resolvedTreeIds = new List<int>(resolvedIds);
            }

            Scribe_Collections.Look(ref treeIds, "ancientTreeIds", LookMode.Value);
            Scribe_Collections.Look(ref workProgress, "ancientTreeWorkProgress", LookMode.Value);
            Scribe_Collections.Look(ref resolvedTreeIds, "ancientTreeResolvedIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                progressByTree.Clear();
                resolvedIds.Clear();
                if (treeIds != null && workProgress != null)
                    for (int i = 0; i < System.Math.Min(treeIds.Count, workProgress.Count); i++)
                        if (treeIds[i] > 0 && workProgress[i] > 0f && !float.IsNaN(workProgress[i]) && !float.IsInfinity(workProgress[i]))
                            progressByTree[treeIds[i]] = workProgress[i];
                if (resolvedTreeIds != null) foreach (int id in resolvedTreeIds) if (id > 0) resolvedIds.Add(id);
                treeIds = new List<int>(); workProgress = new List<float>(); resolvedTreeIds = new List<int>();
            }
        }
    }
}
