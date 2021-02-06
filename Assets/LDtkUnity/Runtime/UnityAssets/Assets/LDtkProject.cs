﻿using System.Collections.Generic;
using LDtkUnity.Providers;
using LDtkUnity.Tools;
using UnityEngine;

namespace LDtkUnity.UnityAssets
{
    [HelpURL(LDtkHelpURL.ASSET_PROJECT)]
    [CreateAssetMenu(fileName = nameof(LDtkProject), menuName = LDtkToolScriptableObj.SO_PATH + "LDtk Project", order = LDtkToolScriptableObj.SO_ORDER)]
    public class LDtkProject : ScriptableObject
    {
        public const string JSON = nameof(_jsonProject);
        public const string LEVEL = nameof(_levels);
        public const string INTGRID = nameof(_intGridValues);
        public const string ENTITIES = nameof(_entities);
        public const string TILESETS = nameof(_tilesets);
        public const string TILEMAP_PREFAB = nameof(_tilemapPrefab);
        public const string INTGRID_VISIBLE = nameof(_intGridValueColorsVisible);
        public const string PIXELS_PER_UNIT = nameof(_pixelsPerUnit);
        
        private const string GRID_PREFAB_PATH = "LDtkDefaultGrid";
        
        [SerializeField] private LDtkProjectFile _jsonProject = null;
        [SerializeField] private Grid _tilemapPrefab = null;
        [SerializeField] private bool _intGridValueColorsVisible = false;
        [SerializeField] private int _pixelsPerUnit = 16;
        [SerializeField] private LDtkLevelFile[] _levels = null;
        [SerializeField] private LDtkIntGridValueAsset[] _intGridValues = null;
        [SerializeField] private LDtkEntityAsset[] _entities = null;
        [SerializeField] private LDtkTilesetAsset[] _tilesets = null;

        public bool IntGridValueColorsVisible => _intGridValueColorsVisible;
        public int PixelsPerUnit => _pixelsPerUnit;
        public LDtkProjectFile ProjectJson => _jsonProject;

        public LDtkLevelFile[] LevelAssets => _levels;

        public LDtkLevelFile GetLevel(string identifier) => GetAssetByIdentifier(_levels, identifier);
        public LDtkIntGridValueAsset GetIntGridValue(string identifier) => GetAssetByIdentifier(_intGridValues, identifier);
        public LDtkEntityAsset GetEntity(string identifier) => GetAssetByIdentifier(_entities, identifier);
        public LDtkTilesetAsset GetTileset(string identifier) => GetAssetByIdentifier(_tilesets, identifier);
        
        private T GetAssetByIdentifier<T>(IEnumerable<T> input, string identifier) where T : ILDtkAsset
        {
            if (LDtkProviderErrorIdentifiers.Contains(identifier))
            {
                //this is to help prevent too much log spam. only one mistake from the same identifier get is necessary.
                return default;
            }
            
            foreach (T asset in input)
            {
                if (ReferenceEquals(asset, null))
                {
                    //Debug.LogError($"LDtk: A field in {name} is null.", this);
                    continue;
                }

                if (asset.Identifier != identifier)
                {
                    continue;
                }

                if (asset.AssetExists)
                {
                    return asset;
                }
                
                Debug.LogError($"LDtk: {asset.Identifier}'s {asset.AssetTypeName} asset was not assigned.", asset.Object);
                return OnFail();
            }

            Debug.LogError($"LDtk: Could not find any asset with identifier \"{identifier}\" in \"{name}\". Unassigned in project assets or identifier misspelling?", this);
            return OnFail();
            
            T OnFail()
            {
                LDtkProviderErrorIdentifiers.Add(identifier);
                return default;
            }
        }


        public Grid GetTilemapPrefab()
        {
            //if override exists, use it. Otherwise use a default. Similar to how Physics Materials resolve empty fields.
            return _tilemapPrefab != null ? _tilemapPrefab : Resources.Load<Grid>(GRID_PREFAB_PATH);
        }
    }
}