﻿using AlienRace;
using Pawnmorph.Hediffs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Pawnmorph.User_Interface
{
    class Dialog_PartPicker : Window
    {
        /// <summary>The pawn that we want to modify.</summary>
        private Pawn pawn;
        private List<Hediff> cachedHediffList;

        // Reference variables
        private const string WINDOW_TITLE_LOC_STRING = "PartPickerMenuTitle";
        private const string DO_SYMMETRY_LOC_STRING = "DoSymmetry";
        private const string SKIN_SYNC_LOC_STRING = "SkinSync";
        private const string NO_MUTATIONS_LOC_STRING = "NoMutationsOnPart";
        private const string EDIT_PARAMS_LOC_STRING = "EditParams";
        private const string TOGGLE_CLOTHES_LOC_STRING = "ToggleClothes";
        private const string ROTATE_CW_LOC_STRING = "RotCW";
        private const string ROTATE_CCW_LOC_STRING = "RotCCW";
        private const string DIF_TITLE_LOC_STRING = "DifTitle";
        private const string PART_DESCRIPTION_TITLE_LOC_STRING = "PartDescTitle";
        private const string APPLY_BUTTON_LOC_STRING = "ApplyButtonText";
        private const string RESET_BUTTON_LOC_STRING = "ResetButtonText";
        private const string CANCEL_BUTTON_LOC_STRING = "CancelButtonText";
        private const float SPACER_SIZE = 17f;
        private const float BUTTON_HORIZONTAL_PADDING = 6f;
        private const float MENU_SECTION_CONSTRICTION_SIZE = 4f;
        private static Vector2 PREVIEW_SIZE = new Vector2(200, 280);
        private static Vector2 TOGGLE_CLOTHES_BUTTON_SIZE = new Vector2(30, 30);
        private static Vector2 ROTATE_CW_BUTTON_SIZE = new Vector2(30, 30);
        private static Vector2 ROTATE_CCW_BUTTON_SIZE = new Vector2(30, 30);
        private static Vector2 APPLY_BUTTON_SIZE = new Vector2(120f, 40f);
        private static Vector2 RESET_BUTTON_SIZE = new Vector2(120f, 40f);
        private static Vector2 CANCEL_BUTTON_SIZE = new Vector2(120f, 40f);

        // Scrolling variables
        private Vector2 partListScrollPos;
        private Vector2 partListScrollSize;
        private Vector2 descriptionScrollPos;
        private Vector2 descriptionScrollSize;

        // Description builders
        private StringBuilder difBuilder = new StringBuilder();
        private StringBuilder partDescBuilder = new StringBuilder();

        // Toggles
        private bool confirmed = false;
        private bool toggleClothesEnabled = true;
        private bool doSymmetry = true;
        private bool skinSync = true;
        private Rot4 previewRot = Rot4.South;

        // Preview related variables
        public bool recachePreview = false;
        private GameObject gameObject;
        private Camera camera;
        private RenderTexture previewImage;

        // Caching variables
        private static Dictionary<BodyPartDef, List<MutationDef>> cachedMutationDefsByPart;
        private static List<BodyPartRecord> cachedMutableParts;

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(750f, 840f);
            }
        }

        public Dialog_PartPicker(Pawn pawn)
        {
            this.pawn = pawn;
            forcePause = true;
            doCloseX = true;
            resizeable = true;
            draggable = true;

            cachedMutationDefsByPart = DefDatabase<MutationDef>.AllDefs.SelectMany(m => m.parts).Distinct().Select(
                k => new {k, v = DefDatabase<MutationDef>.AllDefs.Where(m => m.parts.Contains(k)).ToList()}
                ).ToDictionary(x => x.k, x => x.v);
            cachedMutableParts = pawn.RaceProps.body.AllParts.Where(m => cachedMutationDefsByPart.ContainsKey(m.def)).ToList();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            cachedHediffList = new List<Hediff>(pawn.health.hediffSet.hediffs);
        }

        public override void Close(bool doCloseSound = false)
        {
            if (!confirmed)
            {
                ResetPawnHealth();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            base.Close(doCloseSound);
        }

        public override void OnAcceptKeyPressed()
        {
            confirmed = true;
            base.OnAcceptKeyPressed();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Step 1 - Gather and set relevent information.
            float col1, col2;

            // Step 2 - Draw the title of the window.
            Text.Font = GameFont.Medium;
            string title = $"{WINDOW_TITLE_LOC_STRING.Translate()} - {pawn.Name.ToStringShort} ({pawn.def.LabelCap})";
            float titleHeight = Text.CalcHeight(title, inRect.width);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, Text.CalcHeight(title, inRect.width)), title);
            Text.Font = GameFont.Small;
            col1 = col2 = titleHeight;

            // Step 3 - Determine vewing areas for body part list and description.
            float drawableWidth = (inRect.width - PREVIEW_SIZE.x - 2 * SPACER_SIZE) / 2;
            float drawableHeight = inRect.height - titleHeight - Math.Max(APPLY_BUTTON_SIZE.y, Math.Max(RESET_BUTTON_SIZE.y, CANCEL_BUTTON_SIZE.y)) - 2 * SPACER_SIZE;
            Rect partListOutRect = new Rect(inRect.x, titleHeight, drawableWidth, drawableHeight);
            Rect partListViewRect = new Rect(partListOutRect.x, partListOutRect.y, partListScrollSize.x, partListScrollSize.y);
            Rect previewRect = new Rect(inRect.x + SPACER_SIZE + drawableWidth, titleHeight, PREVIEW_SIZE.x, PREVIEW_SIZE.y);

            // Step 4 - Draw the body part list, selection buttons and edit buttons.
            DrawPartsList(partListOutRect, partListViewRect, ref col1, titleHeight);

            // Step 5 - Draw the preview area...
            if (recachePreview || previewImage == null)
            {
                SetPawnPreview();
            }
            GUI.DrawTexture(previewRect, previewImage);
            col2 += previewRect.height;

            // Then the preview Buttons...
            float rotCWHorPos = previewRect.x + previewRect.width / 2 - TOGGLE_CLOTHES_BUTTON_SIZE.x / 2 - ROTATE_CW_BUTTON_SIZE.x - SPACER_SIZE;
            float toggleClothingHorPos = previewRect.x + previewRect.width / 2 - TOGGLE_CLOTHES_BUTTON_SIZE.x / 2;
            float rotCCWHorPos = previewRect.x + previewRect.width / 2 + TOGGLE_CLOTHES_BUTTON_SIZE.x / 2 + SPACER_SIZE;
            Rect rotCWRect = new Rect(rotCWHorPos, col2, ROTATE_CW_BUTTON_SIZE.x, ROTATE_CW_BUTTON_SIZE.y);
            Rect toggleClothesRect = new Rect(toggleClothingHorPos, col2, TOGGLE_CLOTHES_BUTTON_SIZE.x, TOGGLE_CLOTHES_BUTTON_SIZE.y);
            Rect rotCCWRect = new Rect(rotCCWHorPos, col2, ROTATE_CCW_BUTTON_SIZE.x, ROTATE_CCW_BUTTON_SIZE.y);
            col2 += SPACER_SIZE;
            if (Widgets.ButtonImageFitted(rotCWRect, ButtonTexturesPM.rotCW, Color.white, GenUI.MouseoverColor))
            {
                previewRot.Rotate(RotationDirection.Clockwise);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                recachePreview = true;
            }
            TooltipHandler.TipRegionByKey(rotCWRect, ROTATE_CW_LOC_STRING);
            if (Widgets.ButtonImageFitted(toggleClothesRect, ButtonTexturesPM.toggleClothes, Color.white, GenUI.MouseoverColor))
            {
                toggleClothesEnabled = !toggleClothesEnabled;
                (toggleClothesEnabled ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
                recachePreview = true;
            }
            TooltipHandler.TipRegionByKey(toggleClothesRect, TOGGLE_CLOTHES_LOC_STRING);
            if (Widgets.ButtonImageFitted(rotCCWRect, ButtonTexturesPM.rotCCW, Color.white, GenUI.MouseoverColor))
            {
                previewRot.Rotate(RotationDirection.Counterclockwise);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                recachePreview = true;
            }
            TooltipHandler.TipRegionByKey(rotCCWRect, ROTATE_CCW_LOC_STRING);
            col2 += Math.Max(TOGGLE_CLOTHES_BUTTON_SIZE.y, Math.Max(ROTATE_CW_BUTTON_SIZE.y, ROTATE_CCW_BUTTON_SIZE.y));

            // Then the crown and body type selectors...
            // Head [Type] <-- box that shows selection list.
            // Body [Type] (Need to include these in the reset function.)

            // Then the Aspect selection list...
            // Remember this needs scrolling, Brennen.

            // Then finally the parts list toggles.
            string skinSyncText = SKIN_SYNC_LOC_STRING.Translate();
            Rect skinSyncRect = new Rect(inRect.x + SPACER_SIZE + drawableWidth, col2, PREVIEW_SIZE.x, Text.CalcHeight(skinSyncText, PREVIEW_SIZE.x));
            Widgets.CheckboxLabeled(skinSyncRect, skinSyncText, ref skinSync);
            col2 += skinSyncRect.height;

            string symmetryToggleText = DO_SYMMETRY_LOC_STRING.Translate();
            Rect symmetryToggleRect = new Rect(inRect.x + SPACER_SIZE + drawableWidth, col2, PREVIEW_SIZE.x, Text.CalcHeight(symmetryToggleText, PREVIEW_SIZE.x));
            Widgets.CheckboxLabeled(symmetryToggleRect, symmetryToggleText, ref doSymmetry);

            // Step 6 - Draw description box.
            DrawDescriptionBoxes(new Rect(inRect.x + 2 * SPACER_SIZE + PREVIEW_SIZE.x + partListOutRect.width, titleHeight, drawableWidth, drawableHeight));

            // Step 7 - Draw the apply, reset and cancel buttons.
            float buttonVertPos = titleHeight + drawableHeight + SPACER_SIZE;
            float applyHorPos = inRect.width / 2 - APPLY_BUTTON_SIZE.x - RESET_BUTTON_SIZE.x / 2 - SPACER_SIZE;
            float resetHorPos = inRect.width / 2 - RESET_BUTTON_SIZE.x / 2;
            float cancelHorPos = inRect.width / 2 + RESET_BUTTON_SIZE.x / 2 + SPACER_SIZE;
            if (Widgets.ButtonText(new Rect(applyHorPos, buttonVertPos, APPLY_BUTTON_SIZE.x, APPLY_BUTTON_SIZE.y), APPLY_BUTTON_LOC_STRING.Translate()))
            {
                OnAcceptKeyPressed();
            }
            if (Widgets.ButtonText(new Rect(resetHorPos, buttonVertPos, RESET_BUTTON_SIZE.x, RESET_BUTTON_SIZE.y), RESET_BUTTON_LOC_STRING.Translate()))
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
                ResetPawnHealth();
                recachePreview = true;
            }
            if (Widgets.ButtonText(new Rect(cancelHorPos, buttonVertPos, CANCEL_BUTTON_SIZE.x, CANCEL_BUTTON_SIZE.y), CANCEL_BUTTON_LOC_STRING.Translate()))
            {
                OnCancelKeyPressed();
            }
        }

        public void ResetPawnHealth()
        {
            pawn.health.hediffSet.hediffs = new List<Hediff>(cachedHediffList);
        }

        public void DrawPartsList(Rect outRect, Rect viewRect, ref float curPos, float initialPos)
        {
            List<Hediff_AddedMutation> pawnMutations = pawn.health.hediffSet.hediffs.Where(m => m.def.GetType() == typeof(MutationDef)).Cast<Hediff_AddedMutation>().ToList();
            string editButtonText = EDIT_PARAMS_LOC_STRING.Translate();
            float editButtonWidth = Text.CalcSize(editButtonText).x + BUTTON_HORIZONTAL_PADDING;

            Widgets.BeginScrollView(outRect, ref partListScrollPos, viewRect);
            if (skinSync)
            {
                List<BodyPartRecord> mutableCoreParts = cachedMutableParts.Where(m => DefDatabase<MutationDef>.AllDefs.Where(n => n.RemoveComp.layer == MutationLayer.Core).SelectMany(o => o.parts).Contains(m.def)).ToList();
                DrawPartEntrySkinSyncMode(
                    MutationLayer.Skin.ToString().Translate(),
                    cachedMutableParts.Where(m => DefDatabase<MutationDef>.AllDefs.Where(n => n.RemoveComp.layer == MutationLayer.Skin).SelectMany(o => o.parts).Contains(m.def)).ToList(),
                    ref curPos, viewRect, MutationLayer.Skin);
                if (doSymmetry)
                {
                    foreach (BodyPartDef partDef in mutableCoreParts.Select(m => m.def).Distinct())
                    {
                        List<BodyPartRecord> mutablePartsOfDef = cachedMutableParts.Where(m => m.def == partDef).ToList();
                        DrawPartEntrySkinSyncMode(mutablePartsOfDef.First().def.LabelCap.ToString(), mutablePartsOfDef, ref curPos, viewRect, MutationLayer.Core);
                    }
                }
                else
                {
                    foreach (BodyPartRecord part in mutableCoreParts)
                    {
                        DrawPartEntrySkinSyncMode(part.LabelCap, new List<BodyPartRecord>() { part }, ref curPos, viewRect, MutationLayer.Core);
                    }
                }
            }
            else if (doSymmetry)
            {
                foreach (BodyPartDef part in cachedMutableParts.Select(m => m.def).Distinct())
                {
                    DrawPartEntry(cachedMutableParts.Where(m => m.def == part).ToList(), ref curPos, viewRect);
                }
            }
            else
            {
                foreach (BodyPartRecord part in cachedMutableParts)
                {
                    DrawPartEntry(new List<BodyPartRecord>() {part}, ref curPos, viewRect);
                }
            }
            if (Event.current.type == EventType.Layout)
            {
                partListScrollSize.x = outRect.width - 16f;
                partListScrollSize.y = curPos - initialPos;
            }
            Widgets.EndScrollView();
        }

        private void DrawPartEntry(List<BodyPartRecord> parts, ref float curPos, Rect rect)
        {
            string labelText = doSymmetry ? parts.First().def.LabelCap.ToString() : parts.First().LabelCap.ToString();
            Rect labelRect = new Rect(0f, curPos, rect.width, Text.CalcHeight(labelText, rect.width));
            Widgets.Label(labelRect, labelText);
            curPos += labelRect.height;

            foreach (MutationLayer layer in cachedMutationDefsByPart.Where(m => parts.Select(p => p.def).Contains(m.Key)).SelectMany(n => n.Value).Select(o => o.CompProps<RemoveFromPartCompProperties>().layer).Distinct())
            {
                List<Hediff_AddedMutation> mutations = pawn.health.hediffSet.hediffs
                    .Where(m => m.def.GetType() == typeof(MutationDef))
                    .Cast<Hediff_AddedMutation>()
                    .Where(m => parts.Contains(m.Part) && m.TryGetComp<RemoveFromPartComp>().Layer == layer)
                    .ToList();
                string partButtonText = $"{layer.ToString().Translate()}: {(mutations.NullOrEmpty() ? NO_MUTATIONS_LOC_STRING.Translate().ToString() : string.Join(", ", mutations.Select(m => m.LabelCap).Distinct()))}";
                string editButtonText = EDIT_PARAMS_LOC_STRING.Translate();
                float editButtonWidth = Text.CalcSize(editButtonText).x + BUTTON_HORIZONTAL_PADDING;
                Rect partButtonRect = new Rect(rect.x, curPos, rect.width - editButtonWidth, Text.CalcHeight(partButtonText, rect.width - editButtonWidth - BUTTON_HORIZONTAL_PADDING));
                Rect editButtonRect = new Rect(partButtonRect.width, curPos, editButtonWidth, partButtonRect.height);
                Rect descriptionUpdateRect = new Rect(rect.x, curPos, rect.width, partButtonRect.height);
                if (Widgets.ButtonText(partButtonRect, partButtonText))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    void removeMutations()
                    {
                        foreach (Hediff_AddedMutation mutation in mutations)
                        {
                            pawn.health.RemoveHediff(mutation);
                        }
                        recachePreview = true;
                    }
                    options.Add(new FloatMenuOption(NO_MUTATIONS_LOC_STRING.Translate(), removeMutations));
                    foreach (MutationDef mutationDef in cachedMutationDefsByPart[parts.First().def].Where(m => m.CompProps<RemoveFromPartCompProperties>().layer == layer))
                    {
                        void addMutation()
                        {
                            removeMutations();
                            foreach (BodyPartRecord part in parts)
                            {
                                MutationUtilities.AddMutation(pawn, mutationDef, part);
                            }
                        }
                        options.Add(new FloatMenuOption(mutationDef.LabelCap, addMutation));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                if (Widgets.ButtonText(editButtonRect, editButtonText))
                {
                    // Pop open window to modify current stage, severity, halted status, etc.
                }
                if (Mouse.IsOver(descriptionUpdateRect))
                {
                    foreach (MutationDef mutation in mutations.Select(m => m.Def).Distinct().ToList())
                    {
                        partDescBuilder.AppendLine($"{mutation.LabelCap}");
                        partDescBuilder.AppendLine($"{mutation.description}");
                        partDescBuilder.AppendLine();
                    }
                }
                curPos += partButtonRect.height;
            }
        }

        private void DrawPartEntrySkinSyncMode(string label, List<BodyPartRecord> parts, ref float curPos, Rect rect, MutationLayer layer)
        {
            Rect labelRect = new Rect(rect.x, curPos, rect.width, Text.CalcHeight(label, rect.width));
            Widgets.Label(labelRect, label);
            curPos += labelRect.height;

            List<Hediff_AddedMutation> mutations = pawn.health.hediffSet.hediffs
                .Where(m => m.def.GetType() == typeof(MutationDef))
                .Cast<Hediff_AddedMutation>()
                .Where(m => parts.Contains(m.Part) && m.TryGetComp<RemoveFromPartComp>().Layer == layer)
                .ToList();

            string partButtonText = $"{(mutations.NullOrEmpty() ? NO_MUTATIONS_LOC_STRING.Translate().ToString() : string.Join(", ", mutations.Select(m => m.LabelCap).Distinct()))}";
            string editButtonText = EDIT_PARAMS_LOC_STRING.Translate();

            float editButtonWidth = Text.CalcSize(editButtonText).x + BUTTON_HORIZONTAL_PADDING;
            float partButtonWidth = rect.width - editButtonWidth;
            float partButtonHeight = Text.CalcHeight(partButtonText, partButtonWidth - BUTTON_HORIZONTAL_PADDING);

            Rect partButtonRect = new Rect(rect.x, curPos, partButtonWidth, partButtonHeight);
            Rect editButtonRect = new Rect(partButtonWidth, curPos, editButtonWidth, partButtonHeight);
            Rect descUpdateRect = new Rect(rect.x, curPos, rect.width, partButtonHeight);

            if (Widgets.ButtonText(partButtonRect, partButtonText))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                void removeMutations()
                {
                    foreach (Hediff_AddedMutation mutation in mutations)
                    {
                        pawn.health.RemoveHediff(mutation);
                    }
                    recachePreview = true;
                }
                options.Add(new FloatMenuOption(NO_MUTATIONS_LOC_STRING.Translate(), removeMutations));
                foreach (MutationDef mutationDef in cachedMutationDefsByPart[parts.First().def].Where(m => m.CompProps<RemoveFromPartCompProperties>().layer == layer))
                {
                    void addMutation()
                    {
                        removeMutations();
                        foreach (BodyPartRecord part in parts)
                        {
                            MutationUtilities.AddMutation(pawn, mutationDef, part);
                        }
                    }
                    options.Add(new FloatMenuOption(mutationDef.LabelCap, addMutation));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (Widgets.ButtonText(editButtonRect, editButtonText))
            {
                // Pop open window to modify current stage, severity, halted status, etc.
            }

            if (Mouse.IsOver(descUpdateRect))
            {
                foreach (MutationDef mutation in mutations.Select(m => m.Def).Distinct().ToList())
                {
                    partDescBuilder.AppendLine($"{mutation.LabelCap}");
                    partDescBuilder.AppendLine($"{mutation.description}");
                    partDescBuilder.AppendLine();
                }
            }

            curPos += partButtonHeight;
        }

        public void DrawDescriptionBoxes(Rect inRect)
        {
            float difCurY = 0f;
            Rect difMenuSectionRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height / 2 - SPACER_SIZE);
            Rect difOutRect = difMenuSectionRect.ContractedBy(MENU_SECTION_CONSTRICTION_SIZE);
            Rect difViewRect = new Rect(difOutRect.x, difOutRect.y, descriptionScrollSize.x, descriptionScrollSize.y);
            Rect partDescMenuSectionRect = new Rect(inRect.x, inRect.height / 2 + SPACER_SIZE, inRect.width, inRect.height / 2 + SPACER_SIZE);
            Rect partDescRect = partDescMenuSectionRect.ContractedBy(MENU_SECTION_CONSTRICTION_SIZE);

            Widgets.DrawMenuSection(difMenuSectionRect);
            Widgets.BeginScrollView(difOutRect, ref descriptionScrollPos, difViewRect);
            difCurY += Text.CalcHeight(partDescBuilder.ToString(), difOutRect.width);
            if (Event.current.type == EventType.Layout)
            {
                descriptionScrollSize.x = difOutRect.width - 16f;
                descriptionScrollSize.y = difCurY;
            }
            Widgets.EndScrollView();

            Widgets.DrawMenuSection(partDescMenuSectionRect);
            Widgets.Label(partDescRect, partDescBuilder.ToString());
            partDescBuilder.Clear();
        }

        public void SetPawnPreview()
        {
            if (pawn != null)
            {
                recachePreview = false;
                pawn.Drawer.renderer.graphics.ResolveAllGraphics();
                PortraitsCache.SetDirty(pawn);
                RenderPawn();
                InitCamera();
                camera.gameObject.SetActive(true);
                camera.transform.position = new Vector3(0f, pawn.Position.y + 1f, 0f);
                camera.forceIntoRenderTexture = true;
                camera.targetTexture = previewImage;
                camera.Render();
                camera.targetTexture = null;
                camera.forceIntoRenderTexture = false;
                camera.gameObject.SetActive(false);
            }
            else
            {
                Log.Error("Pawn was null when attempting to recache part picker preview.");
            }
        }

        // Taken from RenderingTool.RenderPawnInternal in CharacterEditor
        private void RenderPawn()
        {
            PawnGraphicSet graphics = pawn.Drawer.renderer.graphics;
            Quaternion quaternion = Quaternion.AngleAxis(0f, Vector3.up);

            Mesh bodyMesh = pawn.RaceProps.Humanlike ? MeshPool.humanlikeBodySet.MeshAt(previewRot) : graphics.nakedGraphic.MeshAt(previewRot);
            Vector3 bodyOffset = new Vector3 (0f, pawn.Position.y + 0.007575758f, 0f);
            foreach (Material mat in graphics.MatsBodyBaseAt(previewRot))
            {
                Material damagedMat = graphics.flasher.GetDamagedMat(mat);
                GenDraw.DrawMeshNowOrLater(bodyMesh, bodyOffset, quaternion, damagedMat, false);
                bodyOffset.y += 0.00390625f;
                if (!toggleClothesEnabled)
                {
                    break;
                }
            }
            Vector3 vector3 = new Vector3(0f, pawn.Position.y + (previewRot == Rot4.North ? 0.026515152f : 0.022727273f), 0f);
            Vector3 vector4 = new Vector3(0f, pawn.Position.y + (previewRot == Rot4.North ? 0.022727273f : 0.026515152f), 0f);
            if (graphics.headGraphic != null)
            {
                Mesh mesh2 = MeshPool.humanlikeHeadSet.MeshAt(previewRot);
                Vector3 headOffset = quaternion * pawn.Drawer.renderer.BaseHeadOffsetAt(previewRot);
                Material material = graphics.HeadMatAt(previewRot);
                GenDraw.DrawMeshNowOrLater(mesh2, vector4 + headOffset, quaternion, material, false);

                Mesh hairMesh = graphics.HairMeshSet.MeshAt(previewRot);
                Vector3 hairOffset = new Vector3(headOffset.x, pawn.Position.y + 0.030303031f, headOffset.z);
                bool isWearingHat = false;
                if (toggleClothesEnabled)
                {
                    foreach (ApparelGraphicRecord apparel in graphics.apparelGraphics)
                    {
                        if (apparel.sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead)
                        {
                            Material hatMat = graphics.flasher.GetDamagedMat(apparel.graphic.MatAt(previewRot));
                            if (apparel.sourceApparel.def.apparel.hatRenderedFrontOfFace)
                            {
                                isWearingHat = true;
                                hairOffset.y += 0.03f;
                                GenDraw.DrawMeshNowOrLater(hairMesh, hairOffset, quaternion, hatMat, false);
                            }
                            else
                            {
                                Vector3 hatOffset = new Vector3(headOffset.x, pawn.Position.y + (previewRot == Rot4.North ? 0.003787879f : 0.03409091f), headOffset.z);
                                GenDraw.DrawMeshNowOrLater(hairMesh, hatOffset, quaternion, hatMat, false);
                            }
                        }
                    }
                }
                if (!isWearingHat)
                {
                    Material hairMat = graphics.HairMatAt(previewRot);
                    GenDraw.DrawMeshNowOrLater(hairMesh, hairOffset, quaternion, hairMat, false);
                }
            }
            if (toggleClothesEnabled)
            {
                foreach (ApparelGraphicRecord graphicsSet in graphics.apparelGraphics)
                {
                    if (graphicsSet.sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Shell)
                    {
                        Material clothingMat = graphics.flasher.GetDamagedMat(graphicsSet.graphic.MatAt(previewRot));
                        GenDraw.DrawMeshNowOrLater(bodyMesh, vector3, quaternion, clothingMat, false);
                    }
                }
            }
            HarmonyPatches.DrawAddons(false, vector3, pawn, quaternion, previewRot, false);
            if (toggleClothesEnabled)
            {
                if (pawn.apparel != null)
                {
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        apparel.DrawWornExtras();
                    }
                }
            }
        }

        internal void InitCamera()
        {
            if (gameObject == null)
            {
                gameObject = new GameObject("PreviewCamera", new Type[]
                {
                    typeof(Camera)
                });
                gameObject.SetActive(false);
            }
            if (camera == null)
            {
                camera = gameObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 1f, 0f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.renderingPath = RenderingPath.Forward;
                camera.nearClipPlane = Current.Camera.nearClipPlane;
                camera.farClipPlane = Current.Camera.farClipPlane;
                camera.targetTexture = null;
                camera.forceIntoRenderTexture = false;
                previewImage = new RenderTexture((int)PREVIEW_SIZE.x, (int)PREVIEW_SIZE.y, 24);
                camera.targetTexture = previewImage;
            }
        }
    }
}
