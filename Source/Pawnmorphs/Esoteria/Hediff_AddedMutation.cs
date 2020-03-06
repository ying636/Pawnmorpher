﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Pawnmorph.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;

namespace Pawnmorph
{
    /// <summary>
    /// hediff representing a mutation 
    /// </summary>
    /// <seealso cref="Verse.HediffWithComps" />
    public class Hediff_AddedMutation : HediffWithComps, IDescriptiveHediff
    {
        [NotNull]
        private readonly Dictionary<int, string> _descCache = new Dictionary<int, string>();

        /// <summary>
        /// checks if this mutation blocks the addition of a new mutation at the given part
        /// </summary>
        /// <param name="otherMutation">The other mutation.</param>
        /// <param name="addPart">The add part.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">otherMutation</exception>
        public virtual bool Blocks([NotNull] MutationDef otherMutation, [CanBeNull] BodyPartRecord addPart)
        {
            if (otherMutation == null) throw new ArgumentNullException(nameof(otherMutation));
            var mDef = def as MutationDef;
            return mDef?.BlocksMutation(otherMutation, Part, addPart) == true; 
        }

        /// <summary>
        /// Gets the influence this mutation confers 
        /// </summary>
        /// <value>
        /// The influence.
        /// </value>
        [NotNull]
        public AnimalClassBase Influence
        {
            get
            {
                if (def is MutationDef mDef)
                {
                    return mDef.classInfluence; 
                }
                else
                {
                    Log.Warning($"{def.defName} is a mutation but does not use {nameof(MutationDef)}! this will cause problems!");
                }

                return AnimalClassDefOf.Animal; 
            }
        }


        /// <summary>
        /// Gets the base label .
        /// </summary>
        /// <value>
        /// The base label .
        /// </value>
        public override string LabelBase
        {
            get
            {
                var lOverride = (CurStage as MutationStage)?.labelOverride;
                var label = string.IsNullOrEmpty(lOverride) ? base.LabelBase : lOverride;

                if (SeverityAdjust?.Halted == true)
                {
                    label += " (halted)"; 
                }

                return label; 

            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>
        /// The description.
        /// </value>
        public virtual string Description
        {
            get
            {
                string desc; 
                if (!_descCache.TryGetValue(CurStageIndex, out desc))
                {
                    StringBuilder builder = new StringBuilder();
                    CreateDescription(builder);
                    desc = builder.ToString();
                    _descCache[CurStageIndex] = desc;

                }
                return desc;
            }
        }

        /// <summary>
        /// The mutation description
        /// </summary>
        public string mutationDescription;


        /// <summary>
        /// Gets a value indicating whether should be removed.
        /// </summary>
        /// <value><c>true</c> if should be removed; otherwise, <c>false</c>.</value>
        public override bool ShouldRemove
        {
            get
            {
                return false;
            }
        }
     
        /// <summary>Gets the extra tip string .</summary>
        /// <value>The extra tip string .</value>
        public override string TipStringExtra
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(base.TipStringExtra);
                stringBuilder.AppendLine("Efficiency".Translate() + ": " + def.addedPartProps.partEfficiency.ToStringPercent());
                return stringBuilder.ToString();
            }
        }

        /// <summary>Creates the description.</summary>
        /// <param name="builder">The builder.</param>
        public virtual void CreateDescription(StringBuilder builder)
        {
            var rawDescription = GetRawDescription(); 
            if (rawDescription == null)
            {
                
                builder.AppendLine("PawnmorphTooltipNoDescription".Translate());
                return;
            }
            
            string res = rawDescription.AdjustedFor(pawn);
            builder.AppendLine(res);
        }

        /// <summary>
        /// Gets the severity adjust comp 
        /// </summary>
        /// <value>
        /// The severity adjust comp
        /// </value>
        [CanBeNull]
        public Comp_MutationSeverityAdjust SeverityAdjust
        {
            get
            {
                if (_sevAdjComp == null)
                {
                    _sevAdjComp = this.TryGetComp<Comp_MutationSeverityAdjust>();
                }

                return _sevAdjComp;
            }
        }


        /// <summary>
        ///     Gets or sets a value indicating whether progression is halted or not.
        /// </summary>
        /// <value>
        ///     <c>true</c> if progression halted; otherwise, <c>false</c>.
        /// </value>
        public bool ProgressionHalted => SeverityAdjust?.Halted == true;

        private string GetRawDescription()
        {
            var descOverride = (CurStage as IDescriptiveStage)?.DescriptionOverride;
            return string.IsNullOrEmpty(descOverride) ? def.description : descOverride; 
        }

        /// <summary>
        /// called every tick 
        /// </summary>
        public override void Tick()
        {
            base.Tick();

            if (_currentStageIndex != CurStageIndex)
            {
                _currentStageIndex = CurStageIndex;
                OnStageChanges(); 
            }
        }

        /// <summary>
        /// Called when the hediff stage changes.
        /// </summary>
        protected virtual void OnStageChanges()
        {
            if (CurStage is IExecutableStage exeStage)
            {
                exeStage.EnteredStage(this); 
            }
        }

        private Comp_MutationSeverityAdjust _sevAdjComp;

        private bool _waitingForUpdate;

        private int _currentStageIndex=-1; 

        /// <summary>called after this instance is added to the pawn.</summary>
        /// <param name="dinfo">The dinfo.</param>
        public override void PostAdd(DamageInfo? dinfo)
        // After the hediff has been applied.
        {
            base.PostAdd(dinfo); // Do the inherited method.
            if (PawnGenerator.IsBeingGenerated(pawn) || !pawn.Spawned) //if the pawn is still being generated do not update graphics until it's done 
            {
                _waitingForUpdate = true;
                return; 
            }
            UpdatePawnInfo();
        }
        /// <summary>
        /// Posts the tick.
        /// </summary>
        public override void PostTick()
        {
            base.PostTick();
            if (_waitingForUpdate)
            {
                UpdatePawnInfo();
                _waitingForUpdate = false; 
            }
        }
        private void UpdatePawnInfo()
        {
           

            if (Current.ProgramState == ProgramState.Playing && MutationUtilities.AllMutationsWithGraphics.Contains(def) && pawn.IsColonist)
            {
                pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                PortraitsCache.SetDirty(pawn);
            }

            pawn.GetMutationTracker()?.NotifyMutationAdded(this);
        }

        /// <summary>called after this instance is removed from the pawn</summary>
        public override void PostRemoved()
        {
            base.PostRemoved();
            if(!PawnGenerator.IsBeingGenerated(pawn))
                pawn.GetMutationTracker()?.NotifyMutationRemoved(this);
        }

        /// <summary>Exposes the data.</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _currentStageIndex, nameof(_currentStageIndex), -1); 
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Part == null)
            {
                Log.Error($"Hediff_AddedPart [{def.defName},{Label}] has null part after loading.", false);
                pawn.health.hediffSet.hediffs.Remove(this);
                return;
            }
        }
        /// <summary>
        /// Restarts the adaption progression for this mutation if halted, does nothing if the part is fully adapted or not halted 
        /// </summary>
        public void ResumeAdaption()
        {
            SeverityAdjust?.Restart();
        }
    }
}
