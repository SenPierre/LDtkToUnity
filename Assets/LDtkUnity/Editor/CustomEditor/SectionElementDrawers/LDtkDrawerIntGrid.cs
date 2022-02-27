﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LDtkUnity.Editor
{
    internal class LDtkDrawerIntGrid : LDtkGroupDrawer<IntGridValueDefinition, LayerDefinition, LDtkDrawerIntGridValue>
    {
        private readonly LDtkDrawerIntGridValueIterator _intGridValueIterator;
        
        public LDtkDrawerIntGrid(LayerDefinition data, SerializedProperty arrayProp, LDtkDrawerIntGridValueIterator intGridValueIterator) : base(data, arrayProp)
        {
            _intGridValueIterator = intGridValueIterator;
            Drawers = GetDrawers().ToList();
        }

        protected override List<LDtkDrawerIntGridValue> GetDrawers()
        {
            return _data.IntGridValues.Select(GetIntGridValueDrawer).ToList();
        }
        
        private LDtkDrawerIntGridValue GetIntGridValueDrawer(IntGridValueDefinition intGridValueDef)
        {
            if (_intGridValueIterator.Value >= ArrayProp.arraySize)
            {
                Debug.LogError("LDtk: Array index out of bounds, the serialized array likely wasn't constructed properly for IntGrid layer");
                return null;
            }
            
            SerializedProperty valueObj = ArrayProp.GetArrayElementAtIndex(_intGridValueIterator.Value);
            _intGridValueIterator.Value++;

            return new LDtkDrawerIntGridValue(intGridValueDef, valueObj, (float) _data.DisplayOpacity);
        }
    }
}