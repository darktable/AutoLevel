﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using AlaslTools;

namespace AutoLevel
{
    using static HandleEx;
    using static FillUtility;

    [CustomEditor(typeof(BlockAsset))]
    public class BlockAssetEditor : BaseRepoEntityEditor
    {
        public class SO : BaseSO<BlockAsset>
        {
            public int group;
            public int weightGroup;

            public List<int> actionsGroups;
            public List<BlockAsset.VariantDesc> variants;

            public SO(SerializedObject serializedObject) : base(serializedObject) { }
            public SO(Object target) : base(target) { }
        }


        private SO blockAsset;
        [SerializeField]
        private int selected;
        private List<BlockAsset.VariantDesc> variants       => blockAsset.variants;
        private List<int> actionsGroups                     => blockAsset.actionsGroups;
        private Transform transform                         => blockAsset.target.transform;
        private BlockAsset.VariantDesc selectedVar          => blockAsset.variants[selected];
        private Vector3 selectedPosition                    => transform.position + selectedVar.position_editor_only;


        private bool connecting = false;
        private int connectingDir;


        private Rect contextMenuRect;
        private HashedFlagList actionsList;
        private ReorderableList variantsReordable;
        private bool variantsReordableChanged;

        #region Callback

        protected override void OnEnable()
        {
            blockAsset = new SO(target);

            base.OnEnable();

            Undo.undoRedoPerformed += UndoCallback;
        }



        protected override void OnDisable()
        {
            base.OnDisable();

            blockAsset.Dispose();

            Undo.undoRedoPerformed -= UndoCallback;
        }

        private void UndoCallback()
        {
            GenerateConnections(activeConnections);
        }

        protected override void Initialize()
        {
            blockAsset.Update();
            CreateVariantsReordable();

            if (initializeCommand.Read(out var data))
                SetSelectedVariant(int.Parse(data));
            else
                SetSelectedVariant(0);

            InitActionsGroups();

            GenerateConnections(activeConnections);
        }

        protected override void Update() { }

        protected override void InspectorGUI()
        {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            int group = GetGroupIndex(blockAsset.group);
            group = EditorGUILayout.Popup("Group", group, allGroups);

            if(EditorGUI.EndChangeCheck())
            {
                blockAsset.group = allGroups[group].GetHashCode();
                blockAsset.ApplyField(nameof(SO.group));
            }

            EditorGUI.BeginChangeCheck();

            int weightGroup = GetWeightGroupIndex(blockAsset.weightGroup);
            weightGroup = EditorGUILayout.Popup("Weight Group", weightGroup, allWeightGroups);

            if (EditorGUI.EndChangeCheck())
            {
                blockAsset.weightGroup = allWeightGroups[weightGroup].GetHashCode();
                blockAsset.ApplyField(nameof(SO.weightGroup));
            }

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            var actionsgroupsExpand = blockAsset.GetFieldExpand(nameof(SO.actionsGroups));
            actionsgroupsExpand = EditorGUILayout.BeginFoldoutHeaderGroup(actionsgroupsExpand, "Actions Groups");
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
                blockAsset.SetFieldExpand(nameof(SO.actionsGroups), actionsgroupsExpand);

            if (actionsgroupsExpand)
                actionsList.Draw(actionsGroups);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label("Variant Settings");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(selected == 0);
            variantsReordable.list = selectedVar.actions;
            variantsReordable.DoLayoutList();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            selectedVar.sideIds.right       = EditorGUILayout.IntField("Right", selectedVar.sideIds.right);
            selectedVar.sideIds.left        = EditorGUILayout.IntField("Left", selectedVar.sideIds.left);
            selectedVar.sideIds.up          = EditorGUILayout.IntField("Up", selectedVar.sideIds.up);
            selectedVar.sideIds.down        = EditorGUILayout.IntField("Down", selectedVar.sideIds.down);
            selectedVar.sideIds.forward     = EditorGUILayout.IntField("Front", selectedVar.sideIds.forward);
            selectedVar.sideIds.backward    = EditorGUILayout.IntField("Back", selectedVar.sideIds.backward);

            EditorGUILayout.Space();

            
            selectedVar.weight = EditorGUILayout.FloatField("Weight", selectedVar.weight);

            EditorGUILayout.Space();

            var layerSettings = selectedVar.layerSettings;

            layerSettings.layer = EditorGUILayout.IntField("Layer", layerSettings.layer);
            if (!layerSettings.PartOfBaseLayer)
            {
                layerSettings.resolve = (BlocksResolve)EditorGUILayout.EnumPopup("Block Resolve", layerSettings.resolve);
                layerSettings.placement = (BlockPlacement)EditorGUILayout.EnumPopup("Block Placement", layerSettings.placement);
            }

            if (EditorGUI.EndChangeCheck() || variantsReordableChanged)
            {
                ApplyVariants();
                SceneView.RepaintAll();
                variantsReordableChanged = false;
            }

            EditorGUILayout.Space();

            if (selectedVar.bigBlock != null && GUILayout.Button("Deattach From Big Block"))
                DetachFromBigBlock(selected);

            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();

            if (selected == 0)
            {
                if (GUILayout.Button("Connection To Variants"))
                    ApplyConnectionsToVariants();

                if (GUILayout.Button("Weight To Variants"))
                    ApplyWeightToVariants();
            }
        }

        protected override void SceneGUI()
        {
            if (cancelDown)
                connecting = false;

            DrawAssetsBlocks();

            if (connecting)
                DrawAssetsBlocksSides(activeRepoEntities.Where((asset) => asset != blockAsset.target));
            else
                DoBlocksSelectionButtons(activeRepoEntities.Where((asset) => asset != blockAsset.target), selectedVar.layerSettings.layer);

            DoBlockToBigBlockConnectionsControls();

            DrawConnections(activeConnections);

            if (settings.DrawVariants)
                DoMyBlocksMoveHandles();

            DoMyBlocks();

            if (settings.EditMode == BlockEditMode.Connection)
                DoConnectionControls();

            Handles.DrawWireCube(selectedPosition + Vector3.one * 0.5f, Vector3.one);

            if (settings.EditMode == BlockEditMode.Fill)
                FillControls();

            if (settings.EditMode == BlockEditMode.Layer)
                LayerControl();

            DoContextMenu();
        }

        #endregion

        private void InitActionsGroups()
        {
            actionsList = new HashedFlagList(
                repo.GetActionsGroupsNames(),
                actionsGroups,
                () => blockAsset.ApplyField(nameof(SO.actionsGroups)));
        }
        private void CreateVariantsReordable()
        {
            variantsReordable = new ReorderableList(variants[selected].actions, typeof(BlockAction));
            variantsReordable.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Actions");
            variantsReordable.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                rect.position += Vector2.up * 2.5f;
                var variant = variants[selected];
                EditorGUI.BeginChangeCheck();
                variant.actions[index] = (BlockAction)EditorGUI.EnumPopup(rect, variant.actions[index]);
                variantsReordableChanged = EditorGUI.EndChangeCheck();
            };
            variantsReordable.onRemoveCallback = (list) =>
            {
                list.list.RemoveAt(list.index);
                variantsReordableChanged = true;
            };
            variantsReordable.onReorderCallback = (ReorderableList list) =>
            {
                variantsReordableChanged = true;
            };
            variantsReordable.onAddCallback = (list) =>
            {
                list.list.Add(BlockAction.RotateX);
                variantsReordableChanged = true;
            };
        }

        private void DetachFromBigBlock(int index)
        {
            DetachFromBigBlock(new AssetBlock(index, blockAsset.target));
            blockAsset.UpdateField(nameof(SO.variants));
            GenerateConnections(activeConnections);

        }
        private void SetSelectedVariant(int index)
        {
            if (index != selected)
            {
                var so = new SerializedObject(this);
                so.FindProperty(nameof(selected)).intValue = index;
                so.ApplyModifiedProperties();
            }
        }
        private void ApplyConnectionsToVariants()
        {
            var src = variants[selected].sideIds;
            for (int i = 0; i < variants.Count; i++)
            {
                if (i == selected)
                    continue;

                var dst = variants[i];
                var sideIds = src;
                for (int j = 0; j < dst.actions.Count; j++)
                    sideIds = ActionsUtility.ApplyAction(sideIds, dst.actions[j]);
                dst.sideIds = sideIds;
            }
            ApplyVariants();
        }
        private void ApplyFillingToVariants()
        {
            var src = variants[selected].fill;
            for (int i = 0; i < variants.Count; i++)
            {
                if (i == selected)
                    continue;

                var dst = variants[i];
                int filling = src;
                for (int j = 0; j < dst.actions.Count; j++)
                    filling = ActionsUtility.ApplyAction(filling, dst.actions[j]);
                dst.fill = filling;
            }
            ApplyVariants();
        }
        private void ApplyWeightToVariants()
        {
            var src = variants[selected].weight;
            for (int i = 0; i < variants.Count; i++)
                variants[i].weight = src;

            ApplyVariants();
        }

        private void DoBlockToBigBlockConnectionsControls()
        {
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].bigBlock != null)
                {
                    var block = new AssetBlock(i, blockAsset.target);
                    DrawBlockToBigBlockConnection(block);
                    if (DoBlockDetachButton(block))
                    {
                        blockAsset.Update();
                        GenerateConnections(activeConnections);
                    }
                }
            }
        }

        private void DoMyBlocksMoveHandles()
        {
            EditorGUI.BeginChangeCheck();
            foreach (var variant in variants.Skip(1))
            {
                variant.position_editor_only = Handles.DoPositionHandle(transform.position + variant.position_editor_only, Quaternion.identity) - transform.position;
            }
            if (EditorGUI.EndChangeCheck())
                ApplyVariants();
        }
        private void DoMyBlocks()
        {
            if (connecting)
            {
                foreach (var blockItem in AssetBlocksIt(blockAsset.target))
                    if (blockItem.Item1.VariantIndex != selected)
                        BlockSidesDC(blockItem.Item1, blockItem.Item2 + Vector3.one * 0.5f).
                            SetColor(BlockSideNormalColor).Draw();
            }
            else
            {
                foreach (var blockItem in AssetBlocksIt(blockAsset.target))
                    if (blockItem.Item1.VariantIndex != selected)
                        if (BlockButton(blockItem.Item1, blockItem.Item2))
                        {
                            SetSelectedVariant(blockItem.Item1.VariantIndex);
                            Repaint();
                        }
            }
        }
        private void DoConnectionControls()
        {
            var target = new AssetBlock(selected, blockAsset.target);

            if (!connecting)
            {
                foreach (var sideitem in BlockSidesIt(target, GetPositionFromBlockAsset(target)))
                {
                    if (BlockSideButton(sideitem.Item1, sideitem.Item2, 0.9f))
                    {
                        SceneView.lastActiveSceneView.ShowNotification(
                        new GUIContent("Use Shift key to select specific face, and use right click to cancel"), 1f);
                        connecting = true;
                        connectingDir = sideitem.Item1.d;
                    }
                }
            }
            else
            {
                var src = new BlockSide(target, connectingDir);

                DoSideConnection(src, GetPositionFromBlockAsset(src.block), () =>
                {
                    connecting = false;
                    blockAsset.Update();
                    Repaint();
                    GenerateConnections(activeConnections);
                });
            }
        }

        private void FillControls()
        {
            var variant = variants[selected];

            if (variant.bigBlock != null)
                return;

            int fill = variant.fill;
            var pos = transform.position + variant.position_editor_only;
            bool didChange = false;

            for (int i = 0; i < 8; i++)
            {
                if ((fill & (1 << i)) > 0)
                {
                    if (!DoSphereToggle(true, pos + nodes[i]))
                    {
                        fill &= ~(1 << i);
                        didChange = true;
                    }
                }
                else
                {
                    if (DoSphereToggle(false, pos + nodes[i]))
                    {
                        fill |= (1 << i);
                        didChange = true;
                    }
                }
            }
            variant.fill = fill;

            if (didChange)
            {
                blockAsset.Apply();
                ApplyFillingToVariants();
            }
        }
        private bool DoSphereToggle(bool value, Vector3 position, float size = 0.1f)
        {
            var draw = GetDrawCmd().SetPrimitiveMesh(PrimitiveType.Sphere).Scale(size).Move(position);
            Button.SetAll(draw);
            Button.normal.SetColor(value ? Color.green : Color.red);
            Button.hover.SetColor(Color.yellow);
            Button.active.SetColor(Color.yellow);
            bool pressed = Button.Draw<SphereD>();
            return pressed ? !value : value;
        }

        private void LayerControl()
        {
            var layerSettings = selectedVar.layerSettings;

            for (int i = 0; i < variants.Count; i++)
            {
                var block = new AssetBlock(i, blockAsset.target);
                foreach(var depBlock in block.layerSettings.dependencies)
                    DrawBlockOutlineConnection(GetBlockPosition(block), GetBlockPosition(depBlock) , NiceColors.Pistachio);
            }

            var targetLayer = layerSettings.layer - 1;

            foreach (var block in GetBlocksIt(AssetType.BlockAsset))
            {
                if (block.Item1.layerSettings.layer == targetLayer)
                {
                    if (BlockButton(block.Item1, block.Item2))
                    {
                        bool contain = layerSettings.dependencies.Contains(block.Item1);
                        if (contain)
                            layerSettings.dependencies.Remove(block.Item1);
                        else
                            layerSettings.dependencies.Add(block.Item1);

                        blockAsset.ApplyField(nameof(SO.variants));
                    }
                }
            }
        }

        private void DoContextMenu()
        {
            Handles.BeginGUI();

            var buttonSize = 20f;
            var offset = 15;

            Bounds bounds = new Bounds(selectedPosition + Vector3.one * 0.5f, Vector3.one);
            var position = BoundsUtility.ClosestCornerToScreenPoint(bounds, new Vector2(SceneView.lastActiveSceneView.position.width, 0));

            var size = new Vector2(130, buttonSize + 5);
            contextMenuRect = new Rect(HandleUtility.WorldToGUIPoint(position) - Vector2.up * size.y
                + new Vector2(offset, -offset), size);

            GUILayout.BeginArea(contextMenuRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("+", GUILayout.Width(buttonSize)))
            {
                GenericMenu addVariantMenu = new GenericMenu();
                for (int i = 0; i < 7; i++)
                {
                    var action = (BlockAction)i;
                    addVariantMenu.AddItem(new GUIContent(action.ToString()), false, () =>
                    {
                        var variant = new BlockAsset.VariantDesc(variants[selected]);
                        variant.actions.Add(action);
                        variant.fill = ActionsUtility.ApplyAction(variant.fill, action);
                        variant.sideIds = ActionsUtility.ApplyAction(variant.sideIds, action);
                        variant.position_editor_only += Vector3.forward * 2f;
                        variants.Add(variant);

                        SetSelectedVariant(variants.Count - 1);
                        addVariantMenu = null;
                        ApplyVariants();
                    });
                    addVariantMenu.ShowAsContext();
                }
            }

            EditorGUI.BeginDisabledGroup(selected == 0);

            if (GUILayout.Button("-", GUILayout.Width(buttonSize)))
            {
                variants.RemoveAt(selected);
                SetSelectedVariant(Mathf.Clamp(selected, 0, variants.Count - 1));
                ApplyVariants();
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(settings.EditMode.ToString()))
            {
                GenericMenu menu = new GenericMenu();
                var values = selectedVar.layerSettings.PartOfBaseLayer ?
                    new BlockEditMode[] { BlockEditMode.None, BlockEditMode.Connection, BlockEditMode.Fill } :
                    new BlockEditMode[] { BlockEditMode.None, BlockEditMode.Connection, BlockEditMode.Layer };

                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    menu.AddItem(new GUIContent(value.ToString()), false, () =>
                    {
                        settings.EditMode = value;
                        settingsSO.Apply();
                    });
                }
                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Handles.EndGUI();

            if (Event.current.type == EventType.Layout)
            {
                if (contextMenuRect.Contains(Event.current.mousePosition))
                {
                    var id = GUIUtility.GetControlID("Context".GetHashCode(), FocusType.Keyboard); ;
                    HandleUtility.AddControl(id, -1);
                }

            }
        }

        private void ApplyVariants()
        {
            blockAsset.ApplyField(nameof(SO.variants));
            GenerateConnections(activeConnections);
        }
    }

}