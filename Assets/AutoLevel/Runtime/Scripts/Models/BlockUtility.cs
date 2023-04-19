﻿using UnityEngine;
using System.Collections.Generic;
using AlaslTools;

namespace AutoLevel
{

    public static class BlockUtility
    {
        public static Mesh GetMesh(GameObject gameObject)
        {
            var mf = gameObject == null ? null : gameObject.GetComponent<MeshFilter>();
            return mf == null ? null : mf.sharedMesh;
        }

        public static Material GetMaterial(GameObject gameObject)
        {
            var mr = gameObject == null ? null : gameObject.GetComponent<MeshRenderer>();
            return mr == null ? null : mr.sharedMaterial;
        }

        public static Mesh GenerateMesh(Mesh mesh, List<BlockAction> actions)
        {
            var m = Object.Instantiate(mesh);
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;

            for (int i = 0; i < actions.Count; i++)
                ActionsUtility.ApplyAction(m, actions[i]);

#if AUTOLEVEL_DEBUG
            m.name += ActionsUtility.GetActionPrefix(actions);
#endif

            m.RecalculateBounds();
            m.RecalculateTangents();

            return m;
        }

        public static int GetSideCompositeId(IBlock block, int side)
        {
            return new XXHash().
                Append(block.baseIds[side]).
                Append(block.layerSettings.PartOfBaseLayer ? FillUtility.GetSide(block.fill, side) : 0);
        }

        public static ConnectionsIds GetCompositeIds(IBlock block)
        {
            var ids = new ConnectionsIds();
            for (int d = 0; d < 6; d++)
                ids[d] = GetSideCompositeId(block, d);
            return ids;
        }

        public static int GenerateHash(IBlock block)
        {
            var mesh = block.baseMesh;

            return new XXHash(1).
                Append(mesh != null ? block.baseMesh.name.GetHashCode() : 0).
                Append(block.hasGameObject ? block.gameObject.GetHashCode() : 0).
                Append(block.compositeIds.GetHashCode()).
                Append(ActionsUtility.GetActionsHash(block.actions)).
                Append(block.layerSettings.layer);
        }
    }

}