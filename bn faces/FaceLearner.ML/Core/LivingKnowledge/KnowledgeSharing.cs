using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Knowledge Sharing System - enables community learning by:
    /// 1. Exporting learned knowledge to shareable files
    /// 2. Importing and merging knowledge from other users
    /// 3. Validating and weighting imported knowledge
    /// 4. Tracking provenance (where knowledge came from)
    /// </summary>
    public class KnowledgeSharing
    {
        #region Constants
        
        private const string EXPORT_VERSION = "FLEARN_SHARE_V1";
        private const string EXPORT_EXTENSION = ".flknow";
        
        // Minimum requirements for export (prevent sharing garbage)
        private const int MIN_EXPERIMENTS_FOR_EXPORT = 50;
        private const int MIN_NODE_USE_COUNT = 5;
        private const float MIN_NODE_SUCCESS_RATE = 0.3f;
        
        // Merge weights
        private const float DEFAULT_IMPORT_TRUST = 0.5f;  // 50% weight for imported knowledge
        private const float MIN_IMPORT_TRUST = 0.1f;
        private const float MAX_IMPORT_TRUST = 0.9f;
        
        #endregion
        
        #region Data Classes
        
        /// <summary>
        /// Metadata about exported knowledge
        /// </summary>
        public class ExportMetadata
        {
            public string Version { get; set; }
            public string ExportId { get; set; }  // Unique ID for this export
            public DateTime ExportDate { get; set; }
            public string ExporterName { get; set; }  // Optional username
            public int TotalExperiments { get; set; }
            public int NodeCount { get; set; }
            public int SharedEntryCount { get; set; }
            public int FeatureBranchCount { get; set; }
            public string Checksum { get; set; }  // For integrity verification
            
            // Quality metrics
            public float AverageSuccessRate { get; set; }
            public float AverageConfidence { get; set; }
            public List<string> TopFeatures { get; set; } = new List<string>();
        }
        
        /// <summary>
        /// Result of a merge operation
        /// </summary>
        public class MergeResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int NodesAdded { get; set; }
            public int NodesUpdated { get; set; }
            public int NodesSkipped { get; set; }
            public int SharedEntriesMerged { get; set; }
            public int FeatureBranchesMerged { get; set; }
            public float QualityScore { get; set; }  // 0-1 quality of imported data
        }
        
        /// <summary>
        /// Portable knowledge node (serializable without references)
        /// </summary>
        [Serializable]
        public class PortableNode
        {
            public string Path { get; set; }
            public int Feature { get; set; }  // FeatureCategory as int
            public string Value { get; set; }
            public Dictionary<int, float> MorphDeltas { get; set; }
            public Dictionary<int, float> MorphVariance { get; set; }
            public int UseCount { get; set; }
            public int SuccessCount { get; set; }
            public float ConfidenceScore { get; set; }
            public float OutcomeVariance { get; set; }
            public float Health { get; set; }
            public List<PortableNode> Children { get; set; }
        }
        
        /// <summary>
        /// Portable shared feature entry
        /// </summary>
        [Serializable]
        public class PortableSharedEntry
        {
            public string FeatureKey { get; set; }
            public Dictionary<int, float> BaseMorphs { get; set; }
            public int LearnCount { get; set; }
            public float Confidence { get; set; }
        }
        
        /// <summary>
        /// Complete exportable knowledge package
        /// </summary>
        [Serializable]
        public class KnowledgePackage
        {
            public ExportMetadata Metadata { get; set; }
            public PortableNode RootNode { get; set; }
            public List<KeyValuePair<string, PortableNode>> FeatureBranches { get; set; }
            public List<PortableSharedEntry> SharedEntries { get; set; }
        }
        
        #endregion
        
        #region Export
        
        /// <summary>
        /// Export knowledge to a shareable file
        /// </summary>
        public static bool Export(HierarchicalKnowledge knowledge, string exportPath, string exporterName = null)
        {
            if (knowledge == null)
            {
                SubModule.Log("[KnowledgeSharing] Export failed: knowledge is null");
                return false;
            }
            
            try
            {
                // Validate minimum requirements
                if (knowledge.TotalExperiments < MIN_EXPERIMENTS_FOR_EXPORT)
                {
                    SubModule.Log($"[KnowledgeSharing] Export failed: need at least {MIN_EXPERIMENTS_FOR_EXPORT} experiments (have {knowledge.TotalExperiments})");
                    return false;
                }
                
                var package = CreatePackage(knowledge, exporterName);
                
                // Serialize to binary
                using (var stream = File.Create(exportPath))
                using (var writer = new BinaryWriter(stream))
                {
                    WritePackage(writer, package);
                }
                
                // Calculate and append checksum
                string checksum = CalculateFileChecksum(exportPath);
                package.Metadata.Checksum = checksum;
                
                SubModule.Log($"[KnowledgeSharing] Exported to {exportPath}");
                SubModule.Log($"  Nodes: {package.Metadata.NodeCount}, Shared: {package.Metadata.SharedEntryCount}");
                SubModule.Log($"  Quality: SuccessRate={package.Metadata.AverageSuccessRate:F2}, Confidence={package.Metadata.AverageConfidence:F2}");
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"[KnowledgeSharing] Export error: {ex.Message}");
                return false;
            }
        }
        
        private static KnowledgePackage CreatePackage(HierarchicalKnowledge knowledge, string exporterName)
        {
            var package = new KnowledgePackage
            {
                Metadata = new ExportMetadata
                {
                    Version = EXPORT_VERSION,
                    ExportId = Guid.NewGuid().ToString("N").Substring(0, 8),
                    ExportDate = DateTime.UtcNow,
                    ExporterName = exporterName ?? "Anonymous",
                    TotalExperiments = knowledge.TotalExperiments,
                },
                FeatureBranches = new List<KeyValuePair<string, PortableNode>>(),
                SharedEntries = new List<PortableSharedEntry>()
            };
            
            // Export root tree (filtered for quality)
            var rootNode = knowledge.GetRootForExport();
            package.RootNode = ConvertToPortable(rootNode, true);
            package.Metadata.NodeCount = CountPortableNodes(package.RootNode);
            
            // Export feature branches
            var branches = knowledge.GetFeatureBranchesForExport();
            foreach (var branch in branches)
            {
                var portableBranch = ConvertToPortable(branch.Value, true);
                if (portableBranch != null)
                {
                    package.FeatureBranches.Add(new KeyValuePair<string, PortableNode>(branch.Key, portableBranch));
                }
            }
            package.Metadata.FeatureBranchCount = package.FeatureBranches.Count;
            
            // Export shared entries
            var shared = knowledge.GetSharedKnowledgeForExport();
            foreach (var entry in shared)
            {
                if (entry.Value.LearnCount >= 3 && entry.Value.Confidence >= 0.2f)
                {
                    package.SharedEntries.Add(new PortableSharedEntry
                    {
                        FeatureKey = entry.Key,
                        BaseMorphs = new Dictionary<int, float>(entry.Value.BaseMorphs),
                        LearnCount = entry.Value.LearnCount,
                        Confidence = entry.Value.Confidence
                    });
                }
            }
            package.Metadata.SharedEntryCount = package.SharedEntries.Count;
            
            // Calculate quality metrics
            CalculateQualityMetrics(package);
            
            return package;
        }
        
        private static PortableNode ConvertToPortable(KnowledgeNode node, bool filterQuality)
        {
            if (node == null) return null;
            
            // Filter out low-quality nodes
            if (filterQuality)
            {
                if (node.UseCount < MIN_NODE_USE_COUNT) return null;
                if (node.SuccessRate < MIN_NODE_SUCCESS_RATE && node.UseCount >= 10) return null;
            }
            
            var portable = new PortableNode
            {
                Path = node.Path,
                Feature = (int)node.Feature,
                Value = node.Value,
                MorphDeltas = new Dictionary<int, float>(node.MorphDeltas),
                MorphVariance = node.MorphVariance != null ? new Dictionary<int, float>(node.MorphVariance) : new Dictionary<int, float>(),
                UseCount = node.UseCount,
                SuccessCount = node.SuccessCount,
                ConfidenceScore = node.ConfidenceScore,
                OutcomeVariance = node.OutcomeVariance,
                Health = node.Health,
                Children = new List<PortableNode>()
            };
            
            // Convert children recursively
            foreach (var child in node.Children)
            {
                var portableChild = ConvertToPortable(child, filterQuality);
                if (portableChild != null)
                {
                    portable.Children.Add(portableChild);
                }
            }
            
            return portable;
        }
        
        private static int CountPortableNodes(PortableNode node)
        {
            if (node == null) return 0;
            int count = 1;
            foreach (var child in node.Children)
            {
                count += CountPortableNodes(child);
            }
            return count;
        }
        
        private static void CalculateQualityMetrics(KnowledgePackage package)
        {
            var allNodes = new List<PortableNode>();
            CollectAllNodes(package.RootNode, allNodes);
            
            if (allNodes.Count > 0)
            {
                package.Metadata.AverageSuccessRate = allNodes
                    .Where(n => n.UseCount > 0)
                    .Average(n => (float)n.SuccessCount / n.UseCount);
                package.Metadata.AverageConfidence = allNodes.Average(n => n.ConfidenceScore);
            }
            
            // Top features
            var featureCounts = new Dictionary<string, int>();
            foreach (var node in allNodes)
            {
                string feature = ((FeatureCategory)node.Feature).ToString();
                if (!featureCounts.ContainsKey(feature))
                    featureCounts[feature] = 0;
                featureCounts[feature]++;
            }
            package.Metadata.TopFeatures = featureCounts
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => kv.Key)
                .ToList();
        }
        
        private static void CollectAllNodes(PortableNode node, List<PortableNode> list)
        {
            if (node == null) return;
            list.Add(node);
            foreach (var child in node.Children)
            {
                CollectAllNodes(child, list);
            }
        }
        
        #endregion
        
        #region Import & Merge
        
        /// <summary>
        /// Import and merge knowledge from a file
        /// </summary>
        public static MergeResult Import(HierarchicalKnowledge target, string importPath, float trustLevel = DEFAULT_IMPORT_TRUST)
        {
            var result = new MergeResult { Success = false };
            
            if (!File.Exists(importPath))
            {
                result.Message = "Import file not found";
                return result;
            }
            
            try
            {
                // Read package
                KnowledgePackage package;
                using (var stream = File.OpenRead(importPath))
                using (var reader = new BinaryReader(stream))
                {
                    package = ReadPackage(reader);
                }
                
                if (package == null)
                {
                    result.Message = "Failed to read package";
                    return result;
                }
                
                // Validate
                if (package.Metadata.Version != EXPORT_VERSION)
                {
                    result.Message = $"Version mismatch: expected {EXPORT_VERSION}, got {package.Metadata.Version}";
                    return result;
                }
                
                // Clamp trust level
                trustLevel = Math.Max(MIN_IMPORT_TRUST, Math.Min(MAX_IMPORT_TRUST, trustLevel));
                
                // Calculate quality score
                result.QualityScore = CalculateImportQuality(package);
                
                // Merge nodes
                MergeNodes(target, package.RootNode, trustLevel, result);
                
                // Merge feature branches
                foreach (var branch in package.FeatureBranches)
                {
                    target.MergeFeatureBranch(branch.Key, ConvertFromPortable(branch.Value), trustLevel);
                    result.FeatureBranchesMerged++;
                }
                
                // Merge shared entries
                foreach (var entry in package.SharedEntries)
                {
                    target.MergeSharedEntry(entry.FeatureKey, entry.BaseMorphs, entry.LearnCount, entry.Confidence, trustLevel);
                    result.SharedEntriesMerged++;
                }
                
                result.Success = true;
                result.Message = $"Merged from '{package.Metadata.ExporterName}' (ID: {package.Metadata.ExportId})";
                
                SubModule.Log($"[KnowledgeSharing] Import successful from {importPath}");
                SubModule.Log($"  Added: {result.NodesAdded}, Updated: {result.NodesUpdated}, Skipped: {result.NodesSkipped}");
                SubModule.Log($"  Shared: {result.SharedEntriesMerged}, Branches: {result.FeatureBranchesMerged}");
                SubModule.Log($"  Quality: {result.QualityScore:F2}, Trust: {trustLevel:F2}");
                
                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"Import error: {ex.Message}";
                SubModule.Log($"[KnowledgeSharing] {result.Message}");
                return result;
            }
        }
        
        private static float CalculateImportQuality(KnowledgePackage package)
        {
            // Quality based on: experiments, success rate, confidence, diversity
            float expScore = Math.Min(1f, package.Metadata.TotalExperiments / 500f);
            float successScore = package.Metadata.AverageSuccessRate;
            float confScore = package.Metadata.AverageConfidence;
            float diversityScore = Math.Min(1f, package.Metadata.NodeCount / 100f);
            
            return (expScore * 0.2f + successScore * 0.4f + confScore * 0.3f + diversityScore * 0.1f);
        }
        
        private static void MergeNodes(HierarchicalKnowledge target, PortableNode sourceNode, float trustLevel, MergeResult result)
        {
            if (sourceNode == null) return;
            
            // Find matching node in target
            var targetNode = target.FindNodeByPath(sourceNode.Path);
            
            if (targetNode == null)
            {
                // New node - add with reduced confidence
                var newNode = ConvertFromPortable(sourceNode);
                newNode.ConfidenceScore *= trustLevel;
                newNode.UseCount = (int)(newNode.UseCount * trustLevel);
                newNode.SuccessCount = (int)(newNode.SuccessCount * trustLevel);
                
                target.AddNodeAtPath(sourceNode.Path, newNode);
                result.NodesAdded++;
            }
            else
            {
                // Existing node - merge deltas
                MergeNodeDeltas(targetNode, sourceNode, trustLevel);
                result.NodesUpdated++;
            }
            
            // Recursively merge children
            foreach (var childNode in sourceNode.Children)
            {
                MergeNodes(target, childNode, trustLevel, result);
            }
        }
        
        private static void MergeNodeDeltas(KnowledgeNode target, PortableNode source, float trustLevel)
        {
            // Weighted average of morph deltas
            float targetWeight = 1f - trustLevel;
            float sourceWeight = trustLevel * ((float)source.SuccessCount / Math.Max(1, source.UseCount));
            
            // Normalize weights
            float totalWeight = targetWeight + sourceWeight;
            targetWeight /= totalWeight;
            sourceWeight /= totalWeight;
            
            foreach (var kv in source.MorphDeltas)
            {
                if (target.MorphDeltas.ContainsKey(kv.Key))
                {
                    // Blend existing with imported
                    target.MorphDeltas[kv.Key] = target.MorphDeltas[kv.Key] * targetWeight + kv.Value * sourceWeight;
                }
                else
                {
                    // New morph - add with reduced weight
                    target.MorphDeltas[kv.Key] = kv.Value * sourceWeight;
                }
            }
            
            // Update statistics (additive with trust scaling)
            target.UseCount += (int)(source.UseCount * trustLevel);
            target.SuccessCount += (int)(source.SuccessCount * trustLevel);
            
            // Recalculate confidence
            target.ConfidenceScore = Math.Min(1f, 
                target.ConfidenceScore * targetWeight + source.ConfidenceScore * sourceWeight);
        }
        
        private static KnowledgeNode ConvertFromPortable(PortableNode portable)
        {
            if (portable == null) return null;
            
            var node = new KnowledgeNode
            {
                Path = portable.Path,
                Feature = (FeatureCategory)portable.Feature,
                Value = portable.Value,
                MorphDeltas = new Dictionary<int, float>(portable.MorphDeltas),
                MorphVariance = new Dictionary<int, float>(portable.MorphVariance),
                UseCount = portable.UseCount,
                SuccessCount = portable.SuccessCount,
                ConfidenceScore = portable.ConfidenceScore,
                OutcomeVariance = portable.OutcomeVariance,
                Health = portable.Health,
                Children = new List<KnowledgeNode>()
            };
            
            foreach (var child in portable.Children)
            {
                var childNode = ConvertFromPortable(child);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }
            
            return node;
        }
        
        #endregion
        
        #region Serialization Helpers
        
        private static void WritePackage(BinaryWriter writer, KnowledgePackage package)
        {
            // Header
            writer.Write(EXPORT_VERSION);
            
            // Metadata
            writer.Write(package.Metadata.ExportId);
            writer.Write(package.Metadata.ExportDate.Ticks);
            writer.Write(package.Metadata.ExporterName ?? "");
            writer.Write(package.Metadata.TotalExperiments);
            writer.Write(package.Metadata.NodeCount);
            writer.Write(package.Metadata.SharedEntryCount);
            writer.Write(package.Metadata.FeatureBranchCount);
            writer.Write(package.Metadata.AverageSuccessRate);
            writer.Write(package.Metadata.AverageConfidence);
            writer.Write(package.Metadata.TopFeatures.Count);
            foreach (var f in package.Metadata.TopFeatures)
                writer.Write(f);
            
            // Root node
            WritePortableNode(writer, package.RootNode);
            
            // Feature branches
            writer.Write(package.FeatureBranches.Count);
            foreach (var branch in package.FeatureBranches)
            {
                writer.Write(branch.Key);
                WritePortableNode(writer, branch.Value);
            }
            
            // Shared entries
            writer.Write(package.SharedEntries.Count);
            foreach (var entry in package.SharedEntries)
            {
                writer.Write(entry.FeatureKey);
                writer.Write(entry.LearnCount);
                writer.Write(entry.Confidence);
                writer.Write(entry.BaseMorphs.Count);
                foreach (var kv in entry.BaseMorphs)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value);
                }
            }
        }
        
        private static void WritePortableNode(BinaryWriter writer, PortableNode node)
        {
            if (node == null)
            {
                writer.Write(false);
                return;
            }
            
            writer.Write(true);
            writer.Write(node.Path ?? "");
            writer.Write(node.Feature);
            writer.Write(node.Value ?? "");
            writer.Write(node.UseCount);
            writer.Write(node.SuccessCount);
            writer.Write(node.ConfidenceScore);
            writer.Write(node.OutcomeVariance);
            writer.Write(node.Health);
            
            // MorphDeltas
            writer.Write(node.MorphDeltas.Count);
            foreach (var kv in node.MorphDeltas)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value);
            }
            
            // MorphVariance
            writer.Write(node.MorphVariance.Count);
            foreach (var kv in node.MorphVariance)
            {
                writer.Write(kv.Key);
                writer.Write(kv.Value);
            }
            
            // Children
            writer.Write(node.Children.Count);
            foreach (var child in node.Children)
            {
                WritePortableNode(writer, child);
            }
        }
        
        private static KnowledgePackage ReadPackage(BinaryReader reader)
        {
            var version = reader.ReadString();
            if (version != EXPORT_VERSION)
            {
                SubModule.Log($"[KnowledgeSharing] Version mismatch: {version}");
                return null;
            }
            
            var package = new KnowledgePackage
            {
                Metadata = new ExportMetadata
                {
                    Version = version,
                    ExportId = reader.ReadString(),
                    ExportDate = new DateTime(reader.ReadInt64()),
                    ExporterName = reader.ReadString(),
                    TotalExperiments = reader.ReadInt32(),
                    NodeCount = reader.ReadInt32(),
                    SharedEntryCount = reader.ReadInt32(),
                    FeatureBranchCount = reader.ReadInt32(),
                    AverageSuccessRate = reader.ReadSingle(),
                    AverageConfidence = reader.ReadSingle(),
                    TopFeatures = new List<string>()
                },
                FeatureBranches = new List<KeyValuePair<string, PortableNode>>(),
                SharedEntries = new List<PortableSharedEntry>()
            };
            
            int topCount = reader.ReadInt32();
            for (int i = 0; i < topCount; i++)
                package.Metadata.TopFeatures.Add(reader.ReadString());
            
            // Root node
            package.RootNode = ReadPortableNode(reader);
            
            // Feature branches
            int branchCount = reader.ReadInt32();
            for (int i = 0; i < branchCount; i++)
            {
                string key = reader.ReadString();
                var node = ReadPortableNode(reader);
                package.FeatureBranches.Add(new KeyValuePair<string, PortableNode>(key, node));
            }
            
            // Shared entries
            int sharedCount = reader.ReadInt32();
            for (int i = 0; i < sharedCount; i++)
            {
                var entry = new PortableSharedEntry
                {
                    FeatureKey = reader.ReadString(),
                    LearnCount = reader.ReadInt32(),
                    Confidence = reader.ReadSingle(),
                    BaseMorphs = new Dictionary<int, float>()
                };
                
                int morphCount = reader.ReadInt32();
                for (int j = 0; j < morphCount; j++)
                {
                    int key = reader.ReadInt32();
                    float val = reader.ReadSingle();
                    entry.BaseMorphs[key] = val;
                }
                
                package.SharedEntries.Add(entry);
            }
            
            return package;
        }
        
        private static PortableNode ReadPortableNode(BinaryReader reader)
        {
            bool hasNode = reader.ReadBoolean();
            if (!hasNode) return null;
            
            var node = new PortableNode
            {
                Path = reader.ReadString(),
                Feature = reader.ReadInt32(),
                Value = reader.ReadString(),
                UseCount = reader.ReadInt32(),
                SuccessCount = reader.ReadInt32(),
                ConfidenceScore = reader.ReadSingle(),
                OutcomeVariance = reader.ReadSingle(),
                Health = reader.ReadSingle(),
                MorphDeltas = new Dictionary<int, float>(),
                MorphVariance = new Dictionary<int, float>(),
                Children = new List<PortableNode>()
            };
            
            // MorphDeltas
            int deltaCount = reader.ReadInt32();
            for (int i = 0; i < deltaCount; i++)
            {
                int key = reader.ReadInt32();
                float val = reader.ReadSingle();
                node.MorphDeltas[key] = val;
            }
            
            // MorphVariance
            int varCount = reader.ReadInt32();
            for (int i = 0; i < varCount; i++)
            {
                int key = reader.ReadInt32();
                float val = reader.ReadSingle();
                node.MorphVariance[key] = val;
            }
            
            // Children
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                var child = ReadPortableNode(reader);
                if (child != null)
                    node.Children.Add(child);
            }
            
            return node;
        }
        
        private static string CalculateFileChecksum(string path)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 16);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Get info about an export file without importing it
        /// </summary>
        public static ExportMetadata GetExportInfo(string path)
        {
            if (!File.Exists(path)) return null;
            
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    var version = reader.ReadString();
                    if (version != EXPORT_VERSION) return null;
                    
                    return new ExportMetadata
                    {
                        Version = version,
                        ExportId = reader.ReadString(),
                        ExportDate = new DateTime(reader.ReadInt64()),
                        ExporterName = reader.ReadString(),
                        TotalExperiments = reader.ReadInt32(),
                        NodeCount = reader.ReadInt32(),
                        SharedEntryCount = reader.ReadInt32(),
                        FeatureBranchCount = reader.ReadInt32(),
                        AverageSuccessRate = reader.ReadSingle(),
                        AverageConfidence = reader.ReadSingle()
                    };
                }
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// List all export files in a directory
        /// </summary>
        public static List<(string path, ExportMetadata info)> ListExports(string directory)
        {
            var results = new List<(string, ExportMetadata)>();
            
            if (!Directory.Exists(directory)) return results;
            
            foreach (var file in Directory.GetFiles(directory, "*" + EXPORT_EXTENSION))
            {
                var info = GetExportInfo(file);
                if (info != null)
                {
                    results.Add((file, info));
                }
            }
            
            return results.OrderByDescending(x => x.Item2.ExportDate).ToList();
        }
        
        #endregion
    }
}
