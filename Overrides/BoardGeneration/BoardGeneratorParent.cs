﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ICities;
using Klyte.Commons.Extensors;
using Klyte.Commons.Utils;
using Klyte.DynamicTextBoards.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Klyte.DynamicTextBoards.Overrides
{

    public abstract class BoardGeneratorParent<BG> : MonoBehaviour, IRedirectable where BG : BoardGeneratorParent<BG>
    {
        public abstract UIDynamicFont DrawFont { get; }
        protected uint lastFontUpdateFrame = SimulationManager.instance.m_currentTickIndex;
        protected static Shader TextShader => DTBResourceLoader.instance.GetLoadedShader("Klyte/DynamicTextBoards/klytetextboards") ?? DistrictManager.instance.m_properties.m_areaNameShader;

        public static BG Instance { get; protected set; }
        public Redirector RedirectorInstance { get; set; }

        protected void BuildSurfaceFont(out UIDynamicFont font, string fontName)
        {
            font = ScriptableObject.CreateInstance<UIDynamicFont>();

            font.material = new Material(Singleton<DistrictManager>.instance.m_properties.m_areaNameFont.material);
            font.shader = TextShader;
            font.baseline = (Singleton<DistrictManager>.instance.m_properties.m_areaNameFont as UIDynamicFont).baseline;
            font.size = (Singleton<DistrictManager>.instance.m_properties.m_areaNameFont as UIDynamicFont).size * 4;
            font.lineHeight = (Singleton<DistrictManager>.instance.m_properties.m_areaNameFont as UIDynamicFont).lineHeight;
            List<string> fontList = new List<string> { fontName };
            fontList.AddRange(DistrictManager.instance.m_properties?.m_areaNameFont?.baseFont?.fontNames?.ToList());
            font.baseFont = Font.CreateDynamicFontFromOSFont(fontList.ToArray(), 64);


            font.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack | MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public void ChangeFont(string newFont)
        {
            List<string> fontList = new List<string>();
            if (newFont != null)
            {
                fontList.Add(newFont);
            }
            fontList.AddRange(DistrictManager.instance.m_properties.m_areaNameFont.baseFont.fontNames.ToList());
            DrawFont.baseFont = Font.CreateDynamicFontFromOSFont(fontList.ToArray(), 64);
            lastFontUpdateFrame = SimulationManager.instance.m_currentTickIndex;
            OnChangeFont(DrawFont.baseFont.name != newFont ? null : newFont);
            OnTextureRebuilt(DrawFont.baseFont);
        }
        protected virtual void OnChangeFont(string fontName) { }

        protected void OnTextureRebuilt(Font obj)
        {
            if (obj == DrawFont.baseFont)
            {
                lastFontUpdateFrame = SimulationManager.instance.m_currentTickIndex;
            }
            OnTextureRebuiltImpl(obj);
        }
        protected abstract void OnTextureRebuiltImpl(Font obj);

        public virtual void Awake()
        {
            Instance = this as BG;
            RedirectorInstance = KlyteMonoUtils.CreateElement<Redirector>(transform);
        }
    }

    public abstract class BoardGeneratorParent<BG, BBC, CC, BRI, BD, BTD, MRT> : BoardGeneratorParent<BG>, ISerializableDataExtension
        where BG : BoardGeneratorParent<BG, BBC, CC, BRI, BD, BTD, MRT>
        where BBC : IBoardBunchContainer<CC, BRI>
        where BD : BoardDescriptorParentXml<BD, BTD>
        where BTD : BoardTextDescriptorParentXml<BTD>
        where CC : CacheControl
        where BRI : BasicRenderInformation, new()
    {
        public abstract int ObjArraySize { get; }




        public static readonly int m_shaderPropColor = Shader.PropertyToID("_Color");
        public static readonly int m_shaderPropEmissive = Shader.PropertyToID("_Emission");
        public abstract void Initialize();


        public static BBC[] m_boardsContainers;


        private readonly float m_pixelRatio = 0.5f;
        //private const float m_scaleY = 1.2f;
        private readonly float m_textScale = 1;
        private readonly Vector2 m_scalingMatrix = new Vector2(0.015f, 0.015f);

        public override void Awake()
        {
            base.Awake();
            Font.textureRebuilt += OnTextureRebuilt;
            Initialize();
            m_boardsContainers = new BBC[ObjArraySize];

            LogUtils.DoLog($"Loading Boards Generator {typeof(BG)}");


        }


        protected Quad2 GetBounds(ref Building data)
        {
            int width = data.Width;
            int length = data.Length;
            Vector2 vector = new Vector2(Mathf.Cos(data.m_angle), Mathf.Sin(data.m_angle));
            Vector2 vector2 = new Vector2(vector.y, -vector.x);
            vector *= width * 4f;
            vector2 *= length * 4f;
            Vector2 a = VectorUtils.XZ(data.m_position);
            Quad2 quad = default;
            quad.a = a - vector - vector2;
            quad.b = a + vector - vector2;
            quad.c = a + vector + vector2;
            quad.d = a - vector + vector2;
            return quad;
        }
        protected Quad2 GetBounds(Vector3 ref1, Vector3 ref2, float halfWidth)
        {
            Vector2 ref1v2 = VectorUtils.XZ(ref1);
            Vector2 ref2v2 = VectorUtils.XZ(ref2);
            float halfLength = (ref1v2 - ref2v2).magnitude / 2;
            Vector2 center = (ref1v2 + ref2v2) / 2;
            float angle = Vector2.Angle(ref1v2, ref2v2);


            Vector2 vector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 vector2 = new Vector2(vector.y, -vector.x);
            vector *= halfWidth;
            vector2 *= halfLength;
            Quad2 quad = default;
            quad.a = center - vector - vector2;
            quad.b = center + vector - vector2;
            quad.c = center + vector + vector2;
            quad.d = center - vector + vector2;
            return quad;
        }



        protected void RenderPropMesh(ref PropInfo propInfo, RenderManager.CameraInfo cameraInfo, ushort refId, int boardIdx, int secIdx, int layerMask, float refAngleRad, Vector3 position, Vector4 dataVector, ref string propName, Vector3 propAngle, Vector3 propScale, ref BD descriptor, out Matrix4x4 propMatrix, out bool rendered)
        {
            if (!string.IsNullOrEmpty(propName))
            {
                if (propInfo == null)
                {
                    propInfo = PrefabCollection<PropInfo>.FindLoaded(propName);
                    if (propInfo == null)
                    {
                        LogUtils.DoErrorLog($"PREFAB NOT FOUND: {propName}");
                        propName = null;
                    }
                }
                propInfo.m_color0 = GetColor(refId, boardIdx, secIdx, descriptor);
            }
            propMatrix = RenderProp(refId, refAngleRad, cameraInfo, propInfo, position, dataVector, boardIdx, layerMask, propAngle, propScale, out rendered);
        }



        #region Rendering
        private Matrix4x4 RenderProp(ushort refId, float refAngleRad, RenderManager.CameraInfo cameraInfo,
#pragma warning disable IDE0060 // Remover o parâmetro não utilizado
                                     PropInfo propInfo, Vector3 position, Vector4 dataVector, int idx, int layerMask,
#pragma warning restore IDE0060 // Remover o parâmetro não utilizado
                                     Vector3 rotation, Vector3 scale, out bool rendered)
        {
            rendered = false;
            //     DistrictManager instance2 = Singleton<DistrictManager>.instance;
            Randomizer randomizer = new Randomizer((refId << 6) | (idx + 32));
            Matrix4x4 matrix = default;
            matrix.SetTRS(position, Quaternion.AngleAxis(rotation.y + (refAngleRad * Mathf.Rad2Deg), Vector3.down) * Quaternion.AngleAxis(rotation.x, Vector3.left) * Quaternion.AngleAxis(rotation.z, Vector3.back), scale);
            if (propInfo != null)
            {
                //scale = propInfo.m_minScale + (float)randomizer.Int32(10000u) * (propInfo.m_maxScale - propInfo.m_minScale) * 0.0001f;
                // byte district = instance2.GetDistrict(position);
                //   byte park = instance2.GetPark(position);
                propInfo = propInfo.GetVariation(ref randomizer);//, park, ref instance2.m_districts.m_buffer[(int)district]);
                Color color = propInfo.m_color0;
                //      float magn = scale.magnitude;
                //if ((layerMask & 1 << propInfo.m_prefabDataLayer) != 0 || propInfo.m_hasEffects)
                //{
                if (cameraInfo.CheckRenderDistance(position, propInfo.m_maxRenderDistance * scale.sqrMagnitude))
                {
                    InstanceID propRenderID2 = GetPropRenderID(refId);
                    int oldLayerMask = cameraInfo.m_layerMask;
                    float oldRenderDist = propInfo.m_lodRenderDistance;
                    propInfo.m_lodRenderDistance *= scale.sqrMagnitude;
                    cameraInfo.m_layerMask = 0x7FFFFFFF;
                    try
                    {
                        PropInstance.RenderInstance(cameraInfo, propInfo, propRenderID2, matrix, position, scale.y, refAngleRad + (rotation.y * Mathf.Deg2Rad), color, dataVector, true);
                    }
                    finally
                    {
                        propInfo.m_lodRenderDistance = oldRenderDist;
                        cameraInfo.m_layerMask = oldLayerMask;
                    }
                    rendered = true;
                }
                //}
            }
            return matrix;
        }

        protected abstract InstanceID GetPropRenderID(ushort refID);

        protected void RenderTextMesh(RenderManager.CameraInfo cameraInfo, MRT refID, int boardIdx, int secIdx, ref BD descriptor, Matrix4x4 propMatrix, ref BTD textDescriptor, ref CC ctrl, MaterialPropertyBlock materialPropertyBlock)
        {
            BRI renderInfo = null;
            UIFont targetFont = null;
            switch (textDescriptor.m_textType)
            {
                case TextType.OwnName:
                    renderInfo = GetOwnNameMesh(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.Fixed:
                    renderInfo = GetFixedTextMesh(ref textDescriptor, refID, out targetFont);
                    break;
                case TextType.StreetPrefix:
                    renderInfo = GetMeshStreetPrefix(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.StreetSuffix:
                    renderInfo = GetMeshStreetSuffix(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.StreetNameComplete:
                    renderInfo = GetMeshFullStreetName(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.BuildingNumber:
                    renderInfo = GetMeshCurrentNumber(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.Custom1:
                    renderInfo = GetMeshCustom1(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.Custom2:
                    renderInfo = GetMeshCustom2(refID, boardIdx, secIdx, out targetFont);
                    break;
                case TextType.Custom3:
                    renderInfo = GetMeshCustom3(refID, boardIdx, secIdx, out targetFont);
                    break;
            }
            if (renderInfo == null || targetFont == null)
            {
                return;
            }

            float overflowScaleX = 1f;
            float overflowScaleY = 1f;
            float defaultMultiplierX = textDescriptor.m_textScale * m_scalingMatrix.x;
            float defaultMultiplierY = textDescriptor.m_textScale * m_scalingMatrix.y;
            float realWidth = defaultMultiplierX * renderInfo.m_sizeMetersUnscaled.x;
            float realHeight = defaultMultiplierY * renderInfo.m_sizeMetersUnscaled.y;
            //doLog($"[{GetType().Name},{refID},{boardIdx},{secIdx}] realWidth = {realWidth}; realHeight = {realHeight}");
            Vector3 targetRelativePosition = textDescriptor.m_textRelativePosition;
            if (textDescriptor.m_maxWidthMeters > 0 && textDescriptor.m_maxWidthMeters < realWidth)
            {
                overflowScaleX = textDescriptor.m_maxWidthMeters / realWidth;
                if (textDescriptor.m_applyOverflowResizingOnY)
                {
                    overflowScaleY = overflowScaleX;
                }
            }
            else
            {
                if (textDescriptor.m_maxWidthMeters > 0 && textDescriptor.m_textAlign != UIHorizontalAlignment.Center)
                {
                    float factor = textDescriptor.m_textAlign == UIHorizontalAlignment.Left == (((textDescriptor.m_textRelativeRotation.y % 360) + 810) % 360 > 180) ? 0.5f : -0.5f;
                    targetRelativePosition += new Vector3((textDescriptor.m_maxWidthMeters - realWidth) * factor / descriptor.ScaleX, 0, 0);
                }
            }
            if (textDescriptor.m_verticalAlign != UIVerticalAlignment.Middle)
            {
                float factor = textDescriptor.m_verticalAlign == UIVerticalAlignment.Bottom == (((textDescriptor.m_textRelativeRotation.x % 360) + 810) % 360 > 180) ? 0.5f : -0.5f;
                targetRelativePosition += new Vector3(0, realHeight * factor, 0);
            }




            Matrix4x4 matrix = propMatrix * Matrix4x4.TRS(
                targetRelativePosition,
                Quaternion.AngleAxis(textDescriptor.m_textRelativeRotation.x, Vector3.left) * Quaternion.AngleAxis(textDescriptor.m_textRelativeRotation.y, Vector3.down) * Quaternion.AngleAxis(textDescriptor.m_textRelativeRotation.z, Vector3.back),
                new Vector3(defaultMultiplierX * overflowScaleX / descriptor.ScaleX, defaultMultiplierY * overflowScaleY / descriptor.PropScale.y, 1));
            if (cameraInfo.CheckRenderDistance(matrix.MultiplyPoint(Vector3.zero), Math.Min(3000, 200 * textDescriptor.m_textScale)))
            {
                if (textDescriptor.m_defaultColor != Color.clear)
                {
                    materialPropertyBlock.SetColor(m_shaderPropColor, textDescriptor.m_defaultColor);
                }
                else if (textDescriptor.m_useContrastColor)
                {
                    materialPropertyBlock.SetColor(m_shaderPropColor, GetContrastColor(refID, boardIdx, secIdx, descriptor));
                }
                else
                {
                    materialPropertyBlock.SetColor(m_shaderPropColor, Color.white);
                }

                materialPropertyBlock.SetFloat(m_shaderPropEmissive, 1.4f * (SimulationManager.instance.m_isNightTime ? textDescriptor.m_nightEmissiveMultiplier : textDescriptor.m_dayEmissiveMultiplier));
                targetFont.material.shader = textDescriptor.ShaderOverride ?? TextShader;
                Graphics.DrawMesh(renderInfo.m_mesh, matrix, targetFont.material, ctrl?.m_cachedProp?.m_prefabDataLayer ?? 10, cameraInfo.m_camera, 0, materialPropertyBlock, false, true, true);
            }
        }

        protected void UpdateMeshStreetSuffix(ushort idx, ref BRI bri)
        {
            LogUtils.DoLog($"!UpdateMeshStreetSuffix {idx}");
            string result = "";
            result = DTBHookable.GetStreetSuffix(idx);
            RefreshNameData(ref bri, result);
        }


        protected void UpdateMeshFullNameStreet(ushort idx, ref BRI bri)
        {
            //(ushort segmentID, ref string __result, ref List<ushort> usedQueue, bool defaultPrefix, bool removePrefix = false)
            string name = DTBHookable.GetStreetFullName(idx);
            LogUtils.DoLog($"!GenName {name} for {idx}");
            RefreshNameData(ref bri, name);
        }



        protected void RefreshNameData(ref BRI result, string name, UIFont overrideFont = null)
        {
            if (result == null)
            {
                result = new BRI();
            }

            UIFontManager.Invalidate(overrideFont ?? DrawFont);
            UIRenderData uirenderData = UIRenderData.Obtain();
            try
            {
                uirenderData.Clear();
                PoolList<Vector3> vertices = uirenderData.vertices;
                PoolList<Color32> colors = uirenderData.colors;
                PoolList<Vector2> uvs = uirenderData.uvs;
                PoolList<int> triangles = uirenderData.triangles;
                using (UIFontRenderer uifontRenderer = (overrideFont ?? DrawFont).ObtainRenderer())
                {

                    float width = 10000f;
                    float height = 900f;
                    uifontRenderer.colorizeSprites = true;
                    uifontRenderer.defaultColor = Color.white;
                    uifontRenderer.textScale = m_textScale;
                    uifontRenderer.pixelRatio = m_pixelRatio;
                    uifontRenderer.processMarkup = true;
                    uifontRenderer.multiLine = false;
                    uifontRenderer.wordWrap = false;
                    uifontRenderer.textAlign = UIHorizontalAlignment.Center;
                    uifontRenderer.maxSize = new Vector2(width, height);
                    uifontRenderer.multiLine = false;
                    uifontRenderer.opacity = 1;
                    uifontRenderer.shadow = false;
                    uifontRenderer.shadowColor = Color.black;
                    uifontRenderer.shadowOffset = Vector2.zero;
                    uifontRenderer.outline = false;
                    Vector2 sizeMeters = uifontRenderer.MeasureString(name) * m_pixelRatio;
                    uifontRenderer.vectorOffset = new Vector3(width * m_pixelRatio * -0.5f, sizeMeters.y * 0.5f, 0f);
                    uifontRenderer.Render(name, uirenderData);
                    result.m_sizeMetersUnscaled = sizeMeters;
                }
                if (result.m_mesh == null)
                {
                    result.m_mesh = new Mesh();
                }
                LogUtils.DoLog(uirenderData.ToString());
                result.m_mesh.Clear();
                result.m_mesh.vertices = vertices.ToArray();
                result.m_mesh.colors32 = colors.Select(x => new Color32(x.a, x.a, x.a, x.a)).ToArray();
                result.m_mesh.uv = uvs.ToArray();
                result.m_mesh.triangles = triangles.ToArray();
                result.m_frameDrawTime = lastFontUpdateFrame;
            }
            finally
            {
                uirenderData.Release();
            }

        }

        #endregion
        public abstract Color GetColor(ushort buildingID, int idx, int secIdx, BD descriptor);
        public abstract Color GetContrastColor(MRT refID, int boardIdx, int secIdx, BD descriptor);

        #region UpdateData
        protected virtual BRI GetOwnNameMesh(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshCurrentNumber(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshFullStreetName(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshStreetSuffix(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshStreetPrefix(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshCustom1(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshCustom2(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetMeshCustom3(MRT refID, int boardIdx, int secIdx, out UIFont targetFont) { targetFont = DrawFont; return null; }
        protected virtual BRI GetFixedTextMesh(ref BTD textDescriptor, MRT refID, out UIFont targetFont)
        {
            targetFont = DrawFont;
            if (textDescriptor.GeneratedFixedTextRenderInfo == null || textDescriptor.GeneratedFixedTextRenderInfoTick < lastFontUpdateFrame)
            {
                BRI result = textDescriptor.GeneratedFixedTextRenderInfo as BRI;
                RefreshNameData(ref result, (textDescriptor.m_isFixedTextLocalized ? Locale.Get(textDescriptor.m_fixedText, textDescriptor.m_fixedTextLocaleKey) : textDescriptor.m_fixedText) ?? "");
                textDescriptor.GeneratedFixedTextRenderInfo = result;
            }
            return textDescriptor.GeneratedFixedTextRenderInfo as BRI;
        }
        #endregion

        #region Serialization
        protected abstract string ID { get; }
        public IManagers Managers => SerializableDataManager?.managers;

        public ISerializableData SerializableDataManager { get; private set; }

        public void OnCreated(ISerializableData serializableData) => SerializableDataManager = serializableData;
        public void OnLoadData()
        {
            if (ID == null || Singleton<ToolManager>.instance.m_properties.m_mode != ItemClass.Availability.Game)
            {
                return;
            }
            if (!SerializableDataManager.EnumerateData().Contains(ID))
            {
                return;
            }
            using MemoryStream memoryStream = new MemoryStream(SerializableDataManager.LoadData(ID));
            byte[] storage = memoryStream.ToArray();
            Deserialize(System.Text.Encoding.UTF8.GetString(storage));
        }

        // Token: 0x0600003B RID: 59 RVA: 0x00004020 File Offset: 0x00002220
        public void OnSaveData()
        {
            if (ID == null || Singleton<ToolManager>.instance.m_properties.m_mode != ItemClass.Availability.Game)
            {
                return;
            }

            string serialData = Serialize();
            LogUtils.DoLog($"serialData: {serialData}");
            byte[] data = System.Text.Encoding.UTF8.GetBytes(serialData);
            SerializableDataManager.SaveData(ID, data);
        }

        public abstract void Deserialize(string data);
        public abstract string Serialize();
        public void OnReleased() { }
        #endregion


        protected static string A_ShaderNameTest = "Klyte/DynamicTextBoards/klytetextboards";
        protected static IEnumerable<string> A_Shaders => DTBShaderLibrary.m_loadedShaders.Keys;

        protected void A_ReloadFromDisk() => DTBShaderLibrary.ReloadFromDisk();
        protected void A_CopyToFont() => DrawFont.shader = DTBResourceLoader.instance.GetLoadedShader(A_ShaderNameTest);

    }


}