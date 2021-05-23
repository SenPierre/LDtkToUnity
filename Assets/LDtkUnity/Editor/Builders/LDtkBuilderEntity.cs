﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LDtkUnity.Editor
{
    public class LDtkBuilderEntity : LDtkLayerBuilder
    {
        public LDtkBuilderEntity(LDtkProjectImporter importer, GameObject layerGameObject, LDtkSortingOrder sortingOrder) : base(importer, layerGameObject, sortingOrder)
        {
        }

        //this is to maintain uniqueness in the import process
        private readonly Dictionary<string, int> _entitiesBuilt = new Dictionary<string, int>();
        
        public void BuildEntityLayerInstances()
        {
            if (Importer.DeparentInRuntime)
            {
                LayerGameObject.AddComponent<LDtkDetachChildren>();
            }
            
            SortingOrder.Next();
            
            LDtkParsedPoint.InformOfRecentLayerVerticalCellCount(Layer.UnityWorldPosition, (int)Layer.CHei);

            foreach (EntityInstance entityData in Layer.EntityInstances)
            {
                GameObject entityPrefab = Importer.GetEntity(entityData.Identifier);
                if (entityPrefab == null)
                {
                    continue;
                }

                BuildEntityInstance(entityData, entityPrefab);
            }
        }

        private void BuildEntityInstance(EntityInstance entityData, GameObject entityPrefab)
        {
            GameObject entityObj = LDtkPrefabFactory.Instantiate(entityPrefab);
            entityObj.name = GetEntityGameObjectName(entityPrefab.name);

            PositionEntity(entityData, entityObj);
            ScaleEntity(entityData, entityObj);
            AddFieldData(entityData, entityObj);

            TryAddImageDrawer(entityData, entityObj);
        }

        private void TryAddImageDrawer(EntityInstance entityData, GameObject entityObj)
        {
            EntityInstanceTile tile = entityData.Tile;
            if (tile == null)
            {
                return;
            }

            LDtkRelativeGetterTilesetTexture textureGetter = new LDtkRelativeGetterTilesetTexture();
            Texture2D tex = textureGetter.GetRelativeAsset(tile.TilesetDefinition, Importer.assetPath);
            if (tex == null)
            {
                return;
            }

            Rect src = tile.UnitySourceRect;

            Vector2Int pos = new Vector2Int((int) src.position.x, (int) src.position.y);
            Vector2Int correctPos = LDtkToolOriginCoordConverter.ImageSliceCoord(pos, tex.height, (int) src.height);
            
            Rect actualRect = new Rect(src)
            {
                position = correctPos,
            };
            
            LDtkEntityIcon icon = entityObj.AddComponent<LDtkEntityIcon>();
            icon.SetValue(tex, actualRect);
        }

        private void AddFieldData(EntityInstance entityData, GameObject entityObj)
        {
            LDtkFieldInjector fieldInjector = new LDtkFieldInjector(entityObj, entityData.FieldInstances);
            fieldInjector.InjectEntityFields();
            
            

            TryAddPointDrawer(fieldInjector.FieldsComponent, entityData, (int)Layer.GridSize);
            


            InterfaceEvents(entityData, entityObj, fieldInjector.FieldsComponent);
        }

        private void InterfaceEvents(EntityInstance entityData, GameObject entityObj, LDtkFields fields)
        {
            MonoBehaviour[] behaviors = entityObj.GetComponents<MonoBehaviour>();
            
            LDtkInterfaceEvent.TryEvent<ILDtkImportedLayer>(behaviors, e => e.OnLDtkImportLayer(Layer));

            if (fields != null)
            {
                LDtkInterfaceEvent.TryEvent<ILDtkImportedFields>(behaviors, e => e.OnLDtkImportFields(fields));
            }

            LDtkInterfaceEvent.TryEvent<ILDtkImportedEntity>(behaviors, e => e.OnLDtkImportEntity(entityData));
            LDtkInterfaceEvent.TryEvent<ILDtkImportedSortingOrder>(behaviors, e => e.OnLDtkImportSortingOrder(SortingOrder.SortingOrderValue));
        }

        private static void ScaleEntity(EntityInstance entityData, GameObject entityObj)
        {
            //modify by the resized entity scaling from LDtk
            Vector3 newScale = entityObj.transform.localScale;
            newScale.Scale(entityData.UnityScale);
            entityObj.transform.localScale = newScale;
        }

        private void PositionEntity(EntityInstance entityData, GameObject entityObj)
        {
            entityObj.transform.parent = LayerGameObject.transform;
            entityObj.transform.localPosition = LDtkToolOriginCoordConverter.EntityLocalPosition(entityData.UnityPx, (int) Layer.LevelReference.PxHei, (int) Layer.GridSize);
        }

        /// <summary>
        /// Reason to give them unique names is to add them to the importer correctly. The importer requires unique identifiers 
        /// </summary>
        private string GetEntityGameObjectName(string identifier)
        {
            if (!_entitiesBuilt.ContainsKey(identifier))
            {
                _entitiesBuilt.Add(identifier, 0);
            }
            
            int amount = _entitiesBuilt[identifier];
            string name = $"{identifier}_{amount}";
            _entitiesBuilt[identifier]++;
            return name;
        }

        private static bool DrawerEligibility(FieldInstance field)
        {
            EditorDisplayMode? mode = field.Definition.EditorDisplayMode;
            
            switch (mode)
            {
                case null:
                    return false;
                case EditorDisplayMode.RadiusGrid:
                case EditorDisplayMode.RadiusPx:
                {
                    if (field.IsInt || field.IsFloat)
                    {
                        return true;
                    }

                    break;
                }
                case EditorDisplayMode.PointPath:
                case EditorDisplayMode.PointStar:
                {
                    if (field.IsPoint)
                    {
                        return true;
                    }

                    break;
                }
            }

            return false;
        }
        
        private static void TryAddPointDrawer(LDtkFields fields, EntityInstance entityData, int gridSize)
        {
            //if none qualify, don't add the drawer component
            if (entityData.FieldInstances.All(fieldInstance => !DrawerEligibility(fieldInstance)))
            {
                return;
            }
            
            LDtkSceneDrawer drawer = fields.gameObject.AddComponent<LDtkSceneDrawer>();

            foreach (FieldInstance fieldInstance in entityData.FieldInstances)
            {
                if (!DrawerEligibility(fieldInstance))
                {
                    continue;
                }

                EditorDisplayMode displayMode = fieldInstance.Definition.EditorDisplayMode;
                LDtkSceneDrawerData data = new LDtkSceneDrawerData(fields, fieldInstance.Identifier, entityData.Definition.UnityColor, displayMode, gridSize);
                drawer.AddReference(data);
            }
        }
    }
}