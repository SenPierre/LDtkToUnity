using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace LDtkUnity.Editor
{
    /// <summary>
    /// This importer is for generating everything that's related to a tileset definition.
    /// This is generated by the project importer.
    /// This has no dependency back to the project importer, only the texture it references.
    /// </summary>
    [HelpURL(LDtkHelpURL.IMPORTER_LDTK_TILESET)]
    [ScriptedImporter(LDtkImporterConsts.TILESET_VERSION, LDtkImporterConsts.TILESET_EXT, LDtkImporterConsts.TILESET_ORDER)]
    internal sealed partial class LDtkTilesetImporter : LDtkJsonImporter<LDtkTilesetFile>
    {
        //public FilterMode _filterMode = FilterMode.Point;
        
        /// <summary>
        /// Holds onto all the standard grid-sized tiles. This serializes the sprite's changed settings between reimports, like pivot or physics shape.
        /// </summary>
        public List<LDtkSpriteRect> _sprites = new List<LDtkSpriteRect>();
        /// <summary>
        /// Any tiles that don't conform width & height to the GridSize.
        /// It's separate because we don't want to draw them in the sprite editor window, or otherwise make them configurable.
        /// Also because they won't have tilemap assets generated for them anyways, as their size wouldn't fit in the tilemap.
        /// </summary>
        public List<LDtkSpriteRect> _additionalTiles = new List<LDtkSpriteRect>();
        
        public SecondarySpriteTexture[] _secondaryTextures;
    
        private Texture2D _tex;
        private string _errorText;

        
        /// <summary>
        /// filled by deserialiing
        /// </summary>
        private LDtkTilesetDefinition _definition;
        private int _pixelsPerUnit = 16;
        private TilesetDefinition _json;
        
        private TextureImporter _srcTextureImporter;
        private LDtkTilesetFile _tilesetFile;
        private string _texturePath;
        
        
        public static string[] _previousDependencies;
        protected override string[] GetGatheredDependencies() => _previousDependencies;
        private static string[] GatherDependenciesFromSourceFile(string path)
        {
            Debug.Log("tileset GatherDependenciesFromSourceFile");

            //this depends on the texture
            LDtkProfiler.BeginSample($"GatherDependenciesFromSourceFile/{Path.GetFileName(path)}");
            string texPath = PathToTexture(path);
            _previousDependencies = string.IsNullOrEmpty(texPath) ? Array.Empty<string>() : new []{texPath};
            LDtkProfiler.EndSample();
            return _previousDependencies;
        }
        

        protected override void Import()
        {
            Debug.Log("tileset def import");

            Profiler.BeginSample("DeserializeAndAssign");
            if (!DeserializeAndAssign())
            {
                Profiler.EndSample();
                FailImport();
                return;
            }
            Profiler.EndSample();
            
            
            Profiler.BeginSample("GetTextureImporterPlatformSettings");
            TextureImporterPlatformSettings platformSettings = GetTextureImporterPlatformSettings();
            Profiler.EndSample();
            
            Profiler.BeginSample("CorrectTheTexture");
            if (CorrectTheTexture(_srcTextureImporter, platformSettings))
            {
                //return because of texture importer corrections. we're going to import a 2nd time
                Profiler.EndSample();
                FailImport();
                return;
            }
            Profiler.EndSample();


            Profiler.BeginSample("GetStandardSpriteRectsForDefinition");
            var rects = ReadSourceRectsFromJsonDefinition(_definition.Def);
            Profiler.EndSample();

            Profiler.BeginSample("UpdateSpriteImportData");
            ReformatRectMetaData(rects);
            Profiler.EndSample();

            TextureGenerationOutput output = PrepareGenerate(platformSettings);

            Texture outputTexture = output.output;
            if (output.sprites.IsNullOrEmpty() && outputTexture == null)
            {
                LDtkDebug.LogWarning("No Sprites or Texture are generated. Possibly because all assets in file are hidden or failed to generate texture.", this);
                return;
            }
            if (!string.IsNullOrEmpty(output.importInspectorWarnings))
            {
                LDtkDebug.LogWarning(output.importInspectorWarnings);
            }
            if (output.importWarnings != null)
            {
                foreach (var warning in output.importWarnings)
                {
                    LDtkDebug.LogWarning(warning);
                }
            }
            if (output.thumbNail == null)
            {
                LDtkDebug.LogWarning("Thumbnail generation fail");
            }
            
            outputTexture.name = AssetName;
            ImportContext.AddObjectToAsset("texture", outputTexture, LDtkIconUtility.LoadTilesetFileIcon());
            ImportContext.AddObjectToAsset("tilesetFile", _tilesetFile, LDtkIconUtility.LoadTilesetIcon());
            
            ImportContext.SetMainObject(outputTexture);

            foreach (Sprite spr in output.sprites)
            {
                AddOffsetToPhysicsShape(spr);
                ImportContext.AddObjectToAsset(spr.name, spr);
            }
        }

        
        
        //todo integrate this into base logic. and only display this asset in the importer inspector if this exists
        private void FailImport()
        {
            FailedImportObject o = ScriptableObject.CreateInstance<FailedImportObject>();
            o.Messages.Add(new ImportInfo(){Message = _errorText, Type = MessageType.Error});
            ImportContext.AddObjectToAsset("failedImport", o, LDtkIconUtility.LoadTilesetFileIcon());
            ImportContext.SetMainObject(o);
        }

        

        private TextureGenerationOutput PrepareGenerate(TextureImporterPlatformSettings platformSettings)
        {
            TextureImporterSettings importerSettings = new TextureImporterSettings();
            _srcTextureImporter.ReadTextureSettings(importerSettings);
            importerSettings.spritePixelsPerUnit = _pixelsPerUnit;
            importerSettings.filterMode = FilterMode.Point;

            NativeArray<Color32> rawData = LoadTex().GetRawTextureData<Color32>();

            return TextureGeneration.Generate(
                ImportContext, rawData, _json.PxWid, _json.PxHei, _sprites.Concat(_additionalTiles).ToArray(),
                platformSettings, importerSettings, string.Empty, _secondaryTextures);
        }

        private TextureImporterPlatformSettings GetTextureImporterPlatformSettings()
        {
            string platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            TextureImporterPlatformSettings platformSettings = _srcTextureImporter.GetPlatformTextureSettings(platform);
            return platformSettings.overridden ? platformSettings : _srcTextureImporter.GetDefaultPlatformTextureSettings();
        }

        private bool DeserializeAndAssign()
        {
            //deserialize first. required for the path to the texture importer 
            try
            {
                _definition = FromJson<LDtkTilesetDefinition>();
                _json = _definition.Def;
                _pixelsPerUnit = _definition.Ppu;
            }
            catch (Exception e)
            {
                _errorText = e.ToString();
                return false;
            }
            
            Profiler.BeginSample("GetTextureImporter");
            //LDtkDebug.LogError($"Path {path} is not valid. Is this tileset asset in a folder relative to the LDtk project file? Ensure that it's relativity is maintained if the project was moved also.");
            //string pathToTex = PathToTexture(assetPath);
            _srcTextureImporter = (TextureImporter)GetAtPath(PathToTexture(assetPath));
            Profiler.EndSample();
            
            if (_srcTextureImporter == null)
            {
                _errorText = $"Tried to build tileset {AssetName}, but the texture importer was not found. Is this tileset asset in a folder relative to the LDtk project file? Ensure that it's relativity is maintained if the project was moved also.";
                //LDtkDebug.LogError($"Tried to build tileset {AssetName}, but the texture importer was not found. Is this tileset asset in a folder relative to the LDtk project file? Ensure that it's relativity is maintained if the project was moved also.");
                return false;
            }

            Profiler.BeginSample("AddTilesetSubAsset");
            _tilesetFile = ReadAssetText();
            Profiler.EndSample();
            
            if (_tilesetFile == null)
            {
                _errorText = "Tried to build tileset, but the tileset json ScriptableObject was null";
                return false;
            }
            
            return true;
        }
        
        /*private void AddGeneratedAssets(AssetImportContext ctx, TextureGenerationOutput output)
        {
            

            var assetName = assetNameGenerator.GetUniqueName(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath),  true, this);
            UnityEngine.Object mainAsset = null;

            RegisterTextureAsset(ctx, output, assetName, ref mainAsset);
            /*RegisterGameObjects(ctx, output, ref mainAsset);
            RegisterAnimationClip(ctx, assetName, output);
            RegisterAnimatorController(ctx, assetName);#1#

            ctx.AddObjectToAsset("AsepriteImportData", _tex);
            ctx.SetMainObject(mainAsset);
        }*/

        /*private void RegisterTextureAsset(AssetImportContext ctx, TextureGenerationOutput output, string assetName, ref UnityEngine.Object mainAsset)
        {
            var registerTextureNameId = string.IsNullOrEmpty(_tex.name) ? "Texture" : _tex.name;

            output.texture.name = assetName;
            ctx.AddObjectToAsset(registerTextureNameId, output.texture, output.thumbNail);
            mainAsset = output.texture;
        }*/

        /// <summary>
        /// Only use when needed, it performs a deserialize. look at optimizing if it's expensive
        /// </summary>
        private static string PathToTexture(string assetPath)
        {
            LDtkRelativeGetterTilesetTexture getter = new LDtkRelativeGetterTilesetTexture();
            string pathFrom = Path.Combine(assetPath, "..");
            pathFrom = LDtkPathUtility.CleanPath(pathFrom);
            string path = getter.GetPath(FromJson<LDtkTilesetDefinition>(assetPath).Def, pathFrom);
            //Debug.Log($"relative from {pathFrom}. path of texture importer was {path}");
            return !File.Exists(path) ? string.Empty : path;
        }

        private void AddOffsetToPhysicsShape(Sprite spr)
        {
            List<Vector2[]> srcShapes = GetSpriteData(spr.name).GetOutlines();
            List<Vector2[]> newShapes = new List<Vector2[]>();
            foreach (Vector2[] srcOutline in srcShapes)
            {
                Vector2[] newOutline = new Vector2[srcOutline.Length];
                for (int ii = 0; ii < srcOutline.Length; ii++)
                {
                    Vector2 point = srcOutline[ii];
                    point += spr.rect.size * 0.5f;
                    newOutline[ii] = point;
                }
                newShapes.Add(newOutline);
            }
            spr.OverridePhysicsShape(newShapes);
        }

        /*private void ForceUpdateSpriteDataNames()
        {
            foreach (LDtkSpriteRect spr in _sprites)
            {
                ForceUpdateSpriteDataName(spr);
            }
        }*/

        private void ForceUpdateSpriteDataName(SpriteRect spr)
        {
            spr.name = $"{AssetName}_{spr.rect.x}_{spr.rect.y}_{spr.rect.width}_{spr.rect.height}";
        }

        private bool CorrectTheTexture(TextureImporter textureImporter, TextureImporterPlatformSettings platformSettings)
        {
            bool issue = false;

            if (platformSettings.maxTextureSize < _json.PxWid || platformSettings.maxTextureSize < _json.PxHei)
            {
                issue = true;
                platformSettings.maxTextureSize = 8192;
                Debug.Log($"The texture {textureImporter.assetPath} maxTextureSize was greater than it's resolution. This was automatically fixed.");
            }

            if (platformSettings.format != TextureImporterFormat.RGBA32)
            {
                issue = true;
                platformSettings.format = TextureImporterFormat.RGBA32;
                Debug.Log($"The texture {textureImporter.assetPath} format was not {TextureImporterFormat.RGBA32}. This was automatically fixed.");
            }

            if (!textureImporter.isReadable)
            {
                issue = true;
                textureImporter.isReadable = true;
                Debug.Log($"The texture {textureImporter.assetPath} was not readable. This was automatically fixed.");
            }

            if (!issue)
            {
                return false;
            }
        
            _tex = null;
            textureImporter.SetPlatformTextureSettings(platformSettings);
            AssetDatabase.ImportAsset(textureImporter.assetPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        private Texture2D LoadTex(bool forceLoad = false)
        {
            //in case the importer was destroyed via file delete
            if (this == null)
            {
                return null;
            }
            
            if (_tex == null || forceLoad)
            {
                _tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PathToTexture(assetPath));
            }
            return _tex;
        }
        
        private LDtkSpriteRect GetSpriteData(GUID guid)
        {
            LDtkSpriteRect data = _sprites.FirstOrDefault(x => x.spriteID == guid);
            Debug.Assert(data != null, $"Sprite data not found for GUID: {guid.ToString()}");
            return data;
        }

        private LDtkSpriteRect GetSpriteData(string spriteName)
        {
            LDtkSpriteRect data = _sprites.FirstOrDefault(x => x.name == spriteName);
            Debug.Assert(data != null, $"Sprite data not found for name: {spriteName}");
            return data;
        }


    }
}