using System;
using System.Collections.Generic;
using System.Xml;
using HeirOfNumenor.Features.TroopStatus;

namespace HeirOfNumenor.Features.CustomResourceSystem
{
    /// <summary>
    /// Condition operators for requirement triggers.
    /// </summary>
    public enum TriggerCondition
    {
        /// <summary>Triggers when value is below threshold.</summary>
        Below,

        /// <summary>Triggers when value is above threshold.</summary>
        Above,

        /// <summary>Triggers when value equals threshold (within tolerance).</summary>
        Equals,

        /// <summary>Triggers when days since satisfaction exceeds threshold.</summary>
        DaysExceeds
    }

    /// <summary>
    /// Defines what effect a requirement has when triggered.
    /// </summary>
    public class RequirementEffect
    {
        /// <summary>Which status to affect.</summary>
        public HoNTroopStatusType TargetStatus { get; set; }

        /// <summary>Amount to modify per day while triggered.</summary>
        public float DailyMagnitude { get; set; }

        /// <summary>One-time magnitude when first triggered.</summary>
        public float TriggerMagnitude { get; set; }

        /// <summary>Message to display when triggered.</summary>
        public string TriggerMessage { get; set; }

        /// <summary>Message to display when resolved.</summary>
        public string ResolveMessage { get; set; }

        public RequirementEffect()
        {
            TargetStatus = HoNTroopStatusType.Frustration;
            DailyMagnitude = 1f;
            TriggerMagnitude = 0f;
        }

        public static RequirementEffect FromXml(XmlNode node)
        {
            var effect = new RequirementEffect();

            // Target status
            string statusStr = node.Attributes?["status"]?.Value ?? "Frustration";
            if (Enum.TryParse<HoNTroopStatusType>(statusStr, true, out var status))
                effect.TargetStatus = status;

            // Magnitudes
            if (float.TryParse(node.Attributes?["daily"]?.Value, out float daily))
                effect.DailyMagnitude = daily;
            if (float.TryParse(node.Attributes?["trigger"]?.Value, out float trigger))
                effect.TriggerMagnitude = trigger;

            // Messages
            effect.TriggerMessage = node.Attributes?["triggerMessage"]?.Value;
            effect.ResolveMessage = node.Attributes?["resolveMessage"]?.Value;

            return effect;
        }
    }

    /// <summary>
    /// Defines a culture's requirement for a specific resource.
    /// </summary>
    public class CultureResourceRequirement
    {
        /// <summary>The resource this requirement tracks.</summary>
        public string ResourceId { get; set; }

        /// <summary>Condition for triggering the requirement.</summary>
        public TriggerCondition Condition { get; set; }

        /// <summary>Threshold value for the condition.</summary>
        public float Threshold { get; set; }

        /// <summary>Grace period in days before effects start.</summary>
        public float GracePeriodDays { get; set; }

        /// <summary>Effects when requirement is not met.</summary>
        public List<RequirementEffect> Effects { get; set; }

        /// <summary>Priority for display/processing order.</summary>
        public int Priority { get; set; }

        /// <summary>Whether this requirement is enabled.</summary>
        public bool IsEnabled { get; set; }

        public CultureResourceRequirement()
        {
            Effects = new List<RequirementEffect>();
            Condition = TriggerCondition.Below;
            Threshold = 30f;
            GracePeriodDays = 0f;
            Priority = 0;
            IsEnabled = true;
        }

        public static CultureResourceRequirement FromXml(XmlNode node)
        {
            var req = new CultureResourceRequirement();

            req.ResourceId = node.Attributes?["resource"]?.Value ?? "";
            
            // Condition
            string condStr = node.Attributes?["condition"]?.Value ?? "Below";
            if (Enum.TryParse<TriggerCondition>(condStr, true, out var cond))
                req.Condition = cond;

            // Threshold
            if (float.TryParse(node.Attributes?["threshold"]?.Value, out float threshold))
                req.Threshold = threshold;

            // Grace period
            if (float.TryParse(node.Attributes?["graceDays"]?.Value, out float grace))
                req.GracePeriodDays = grace;

            // Priority
            if (int.TryParse(node.Attributes?["priority"]?.Value, out int priority))
                req.Priority = priority;

            // Enabled
            string enabledStr = node.Attributes?["enabled"]?.Value ?? "true";
            req.IsEnabled = enabledStr.ToLower() != "false";

            // Effects
            foreach (XmlNode effectNode in node.SelectNodes("Effect"))
            {
                req.Effects.Add(RequirementEffect.FromXml(effectNode));
            }

            // Shorthand for simple single effect
            if (req.Effects.Count == 0)
            {
                string statusStr = node.Attributes?["status"]?.Value;
                string magnitudeStr = node.Attributes?["magnitude"]?.Value;
                
                if (!string.IsNullOrEmpty(statusStr))
                {
                    var effect = new RequirementEffect();
                    if (Enum.TryParse<HoNTroopStatusType>(statusStr, true, out var status))
                        effect.TargetStatus = status;
                    if (float.TryParse(magnitudeStr, out float mag))
                        effect.DailyMagnitude = mag;
                    effect.TriggerMessage = node.Attributes?["message"]?.Value;
                    req.Effects.Add(effect);
                }
            }

            return req;
        }
    }

    /// <summary>
    /// Collection of all resource requirements for a culture.
    /// </summary>
    public class CultureResourceProfile
    {
        /// <summary>Culture ID this profile applies to.</summary>
        public string CultureId { get; set; }

        /// <summary>Display name for the culture.</summary>
        public string DisplayName { get; set; }

        /// <summary>All requirements for this culture.</summary>
        public List<CultureResourceRequirement> Requirements { get; set; }

        /// <summary>Whether this profile inherits from a base profile.</summary>
        public string InheritsFrom { get; set; }

        public CultureResourceProfile()
        {
            Requirements = new List<CultureResourceRequirement>();
        }

        public static CultureResourceProfile FromXml(XmlNode node)
        {
            var profile = new CultureResourceProfile();

            profile.CultureId = node.Attributes?["id"]?.Value ?? "";
            profile.DisplayName = node.Attributes?["name"]?.Value ?? profile.CultureId;
            profile.InheritsFrom = node.Attributes?["inherits"]?.Value;

            foreach (XmlNode reqNode in node.SelectNodes("Requirement"))
            {
                profile.Requirements.Add(CultureResourceRequirement.FromXml(reqNode));
            }

            return profile;
        }

        /// <summary>
        /// Merges requirements from a parent profile.
        /// </summary>
        public void InheritFrom(CultureResourceProfile parent)
        {
            if (parent == null) return;

            // Add parent requirements that we don't override
            var existingResources = new HashSet<string>();
            foreach (var req in Requirements)
                existingResources.Add(req.ResourceId);

            foreach (var parentReq in parent.Requirements)
            {
                if (!existingResources.Contains(parentReq.ResourceId))
                {
                    Requirements.Add(parentReq);
                }
            }
        }
    }
}
