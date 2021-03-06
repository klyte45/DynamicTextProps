﻿using ColossalFramework.Globalization;
using ColossalFramework.Math;
using Klyte.Commons.Extensors;
using Klyte.Commons.Utils;
using Klyte.DynamicTextProps.Data;
using Klyte.DynamicTextProps.Libraries;
using Klyte.DynamicTextProps.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Klyte.Commons.Utils.SegmentUtils;

namespace Klyte.DynamicTextProps.Overrides
{

    public partial class BoardGeneratorHighwayMileage : BoardGeneratorParent<BoardGeneratorHighwayMileage, IBoardBunchContainer<CacheControl>, DTPHighwayMileageData, BoardDescriptorMileageMarkerXml, BoardTextDescriptorMileageMarkerXml>
    {

        public BasicRenderInformation[] m_cachedKilometerMeshes;
        public BasicRenderInformation[] m_cachedDirectionMeshes;
        public Tuple<RoadIdentifier, MileageMarkerDescriptor, MileageMarkerDescriptor>[] m_segmentCachedInfo;

        public static readonly TextType[] AVAILABLE_TEXT_TYPES = new TextType[]
          {
                    TextType.OwnName,
                    TextType.Fixed,
                    TextType.Custom1,
                    TextType.BuildingNumber
          };




        internal static void GenerateDefaultSignModelAtLibrary()
        {
            var defaultModel = new BoardDescriptorMileageMarkerXml
            {
                m_propName = "1679681061.Mileage Marker_Data",
                m_textDescriptors = new BoardTextDescriptorMileageMarkerXml[]{
                    new BoardTextDescriptorMileageMarkerXml{
                        SaveName = "Direction",
                        m_textRelativePosition =new Vector3(0f,1.78f,0.066f) ,
                        m_maxWidthMeters = 0.45f,
                        m_textScale = .375f,
                        m_useContrastColor = false,
                        m_textRelativeRotation = new Vector3(0,180,0),
                        m_textType = TextType.Custom1
                    },
                    new BoardTextDescriptorMileageMarkerXml{
                        SaveName = "Kilometer Label",
                        m_textRelativePosition =new Vector3(0f,1.48f,0.066f) ,
                        m_textRelativeRotation = new Vector3(0,180,0),
                        m_maxWidthMeters = 0.45f,
                        m_textScale = .5f,
                        m_useContrastColor = false,
                        m_textType = TextType.Fixed,
                        m_fixedText = "km"
                    },
                    new BoardTextDescriptorMileageMarkerXml{
                        SaveName = "Kilometer",
                        m_textRelativePosition =new Vector3(0f,1.18f,0.066f) ,
                        m_textRelativeRotation = new Vector3(0,180,0),
                        m_maxWidthMeters = 0.45f,
                        m_textScale = .5f,
                        m_useContrastColor = false,
                        m_textType = TextType.BuildingNumber
                    },
                }
            };


            DTPLibMileageMarkerGroup.Instance.Add("<DEFAULT>", defaultModel);
        }
        #region Initialize
        public override void Initialize()
        {
            m_segmentCachedInfo = new Tuple<RoadIdentifier, MileageMarkerDescriptor, MileageMarkerDescriptor>[NetManager.MAX_SEGMENT_COUNT];
            m_cachedKilometerMeshes = new BasicRenderInformation[100];
            m_cachedDirectionMeshes = new BasicRenderInformation[9];

            NetManagerOverrides.EventNodeChanged += OnNodeChanged;
            NetManagerOverrides.EventSegmentChanged += OnSegmentChanged;

            LocaleManager.eventLocaleChanged += () => m_cachedCardinalName = new string[10];

            #region Hooks
            System.Reflection.MethodInfo postRenderMeshs = GetType().GetMethod("AfterRenderNode", RedirectorUtils.allFlags);
            System.Reflection.MethodInfo afterRenderSegment = GetType().GetMethod("AfterRenderSegment", RedirectorUtils.allFlags);
            System.Reflection.MethodInfo onApplyModificationTool = GetType().GetMethod("OnApplyModificationTool", RedirectorUtils.allFlags);
            LogUtils.DoLog($"Patching=> {postRenderMeshs} {postRenderMeshs.IsStatic}");
            AddRedirect(typeof(RoadBaseAI).GetMethod("RenderNode", RedirectorUtils.allFlags), null, postRenderMeshs);
            AddRedirect(typeof(NetSegment).GetMethod("RenderInstance", new Type[] { typeof(RenderManager.CameraInfo), typeof(ushort), typeof(int) }), null, afterRenderSegment);
            AddRedirect(typeof(NetAdjust).GetMethod("ApplyModification", new Type[] { typeof(int) }), null, onApplyModificationTool);
            #endregion
        }



        protected override void ResetImpl() => SoftReset();

        public void SoftReset()
        {
            m_cachedDirectionMeshes = new BasicRenderInformation[9];
            m_cachedKilometerMeshes = new BasicRenderInformation[100];
            m_segmentCachedInfo = new Tuple<RoadIdentifier, MileageMarkerDescriptor, MileageMarkerDescriptor>[NetManager.MAX_SEGMENT_COUNT];
            m_cachedPropInfo = null;
        }

        private void OnNodeChanged(ushort nodeId)
        {
            //doLog($"onNodeChanged { System.Environment.StackTrace }");
            for (int i = 0; i < 8; i++)
            {
                ushort segmentId = NetManager.instance.m_nodes.m_buffer[nodeId].GetSegment(i);
                //if (NetManager.instance.m_segments.m_buffer[segmentId].Info.m_netAI is RoadBaseAI)
                //{
                //    onDistrictChanged();
                //    return;
                //}
                if (segmentId > 0)
                {
                    OnSegmentChanged(segmentId);
                }
            }
        }

        private static readonly Func<NetAdjust, InstanceID> m_getLastInstanceAdjust = ReflectionUtils.GetGetFieldDelegate<NetAdjust, InstanceID>("m_lastInstance", typeof(NetAdjust));

#pragma warning disable IDE0051 // Remover membros privados não utilizados
        private static void OnApplyModificationTool(ref NetAdjust __instance) => Instance.OnSegmentChanged(m_getLastInstanceAdjust(__instance).NetSegment);
#pragma warning restore IDE0051 // Remover membros privados não utilizados

        private void OnSegmentChanged(ushort segmentId) => m_segmentCachedInfo = new Tuple<RoadIdentifier, MileageMarkerDescriptor, MileageMarkerDescriptor>[NetManager.MAX_SEGMENT_COUNT];
        #endregion

        public static void AfterRenderNode(ushort nodeID)
        {

            Instance.ComputeNode(nodeID);

        }
        public static void AfterRenderSegment(RenderManager.CameraInfo cameraInfo, ushort segmentID, int layerMask)
        {

            Instance.AfterRenderSegmentImpl(cameraInfo, segmentID, layerMask);

        }
        public void ComputeNode(ushort nodeID)
        {

            for (int i = 0; i < 8; i++)
            {
                ushort segmentId = NetManager.instance.m_nodes.m_buffer[nodeID].GetSegment(i);
                if (segmentId == 0)
                {
                    continue;
                }

                if (!(NetManager.instance.m_segments.m_buffer[segmentId].Info.m_netAI is RoadBaseAI baseAI) || !baseAI.m_highwayRules)
                {
                    m_segmentCachedInfo[segmentId] = Tuple.New<RoadIdentifier, MileageMarkerDescriptor, MileageMarkerDescriptor>(default, default, default);
                    continue;
                }
                if (m_segmentCachedInfo[segmentId] == null)
                {
                    int divisor = Data.CurrentDescriptor.UseMiles ? 1609 : 1000;
                    IEnumerable<Tuple<ushort, float>> segments = SegmentUtils.GetSegmentRoadEdges(segmentId, false, false, false, out ComparableRoad start, out ComparableRoad end);
                    if (segments == null)
                    {
                        MileageMarkerDescriptor defaultMM = default;
                        var identifier = new RoadIdentifier(default, default, new ushort[] { segmentId });
                        m_segmentCachedInfo[segmentId] = Tuple.NewRef(ref identifier, ref defaultMM, ref defaultMM);
                    }
                    else
                    {
                        var tupleIdentifier = new RoadIdentifier(start, end, segments.Select(x => x.First).ToArray());
                        bool roadInverted = tupleIdentifier.start.nodeReference == NetManager.instance.m_segments.m_buffer[tupleIdentifier.segments[0]].m_endNode == ((NetManager.instance.m_segments.m_buffer[tupleIdentifier.segments[0]].m_flags & NetSegment.Flags.Invert) == 0);
                        var entry = new List<MileageMarkerDescriptor>();
                        float meterCount = 0f;
                        foreach (Tuple<ushort, float> segmentRef in segments)
                        {
                            int oldMeterKm = (int)meterCount / divisor;
                            meterCount += segmentRef.Second;
                            if (oldMeterKm != (int)meterCount / divisor)
                            {
                                NetSegment segmentObj = NetManager.instance.m_segments.m_buffer[segmentRef.First];
                                bool invert = (NetManager.instance.m_segments.m_buffer[segmentRef.First].m_flags & NetSegment.Flags.Invert) > 0;
                                NetManager.instance.m_segments.m_buffer[segmentRef.First].GetClosestPositionAndDirection(NetManager.instance.m_segments.m_buffer[segmentRef.First].m_middlePosition, out Vector3 pos, out Vector3 dir);
                                float rotation = dir.GetAngleXZ();
                                if (invert)
                                {
                                    rotation += 180;
                                }

                                byte cardinalDirection = SegmentUtils.GetCardinalDirection(start, end);
                                if (roadInverted)
                                {
                                    cardinalDirection = (byte)((cardinalDirection + 4) % 8);
                                }
                                var marker1 = new MileageMarkerDescriptor
                                {
                                    segmentId = segmentRef.First,
                                    kilometer = oldMeterKm + 1,
                                    position = segmentObj.m_middlePosition + (VectorUtils.X_Y(KlyteMathUtils.DegreeToVector2(rotation - 90)) * (segmentObj.Info.m_halfWidth - 1)),
                                    cardinalDirection8 = cardinalDirection,
                                    rotation = rotation + 90

                                };
                                if (segmentObj.Info.m_backwardVehicleLaneCount + segmentObj.Info.m_forwardVehicleLaneCount >= 2)
                                {
                                    var marker2 = new MileageMarkerDescriptor
                                    {
                                        segmentId = segmentRef.First,
                                        kilometer = oldMeterKm + 1,
                                        position = segmentObj.m_middlePosition + (VectorUtils.X_Y(KlyteMathUtils.DegreeToVector2(rotation + 90)) * (segmentObj.Info.m_halfWidth - 1)),
                                        cardinalDirection8 = (byte)((cardinalDirection + (segmentObj.Info.m_hasBackwardVehicleLanes && segmentObj.Info.m_hasForwardVehicleLanes ? 4 : 0)) % 8),
                                        rotation = segmentObj.Info.m_hasBackwardVehicleLanes && segmentObj.Info.m_hasForwardVehicleLanes ? rotation - 90 : rotation + 90
                                    };

                                    m_segmentCachedInfo[segmentRef.First] = Tuple.NewRef(ref tupleIdentifier, ref marker1, ref marker2);
                                }
                                else
                                {
                                    MileageMarkerDescriptor defaultMM = default;

                                    m_segmentCachedInfo[segmentRef.First] = Tuple.NewRef(ref tupleIdentifier, ref marker1, ref defaultMM);
                                }
                            }
                            else
                            {
                                MileageMarkerDescriptor defaultMM = default;
                                m_segmentCachedInfo[segmentRef.First] = Tuple.NewRef(ref tupleIdentifier, ref defaultMM, ref defaultMM);
                            }
                        }
                    }
                }
            }

        }
        public void AfterRenderSegmentImpl(RenderManager.CameraInfo cameraInfo, ushort segmentID, int layerMask)
        {

            if (m_segmentCachedInfo[segmentID] == null)
            {
                if (segmentID == 0)
                {
                    return;
                }

                ComputeNode(NetManager.instance.m_segments.m_buffer[segmentID].m_startNode);
            }

            var markers = new MileageMarkerDescriptor[] { m_segmentCachedInfo[segmentID]?.Second ?? default, m_segmentCachedInfo[segmentID]?.Third ?? default };
            for (int i = 0; i < markers.Length; i++)
            {
                MileageMarkerDescriptor marker = markers[i];
                if (marker.segmentId == default)
                {
                    continue;
                }

                RenderPropMesh(ref m_cachedPropInfo, cameraInfo, segmentID, i, marker.kilometer, layerMask, 0, marker.position, Vector4.zero, ref Data.CurrentDescriptor.m_propName, new Vector3(0, marker.rotation) + Data.CurrentDescriptor.m_propRotation, Data.CurrentDescriptor.PropScale, Data.CurrentDescriptor, out Matrix4x4 propMatrix, out bool rendered);
                if (rendered)
                {
                    for (int j = 0; j < Data.CurrentDescriptor.m_textDescriptors.Length; j++)
                    {
                        CacheControl c = null;
                        MaterialPropertyBlock block = NetManager.instance.m_materialBlock;
                        block.Clear();
                        RenderTextMesh(cameraInfo, segmentID, i, marker.kilometer, Data.CurrentDescriptor, propMatrix, Data.CurrentDescriptor.m_textDescriptors[j], block);
                    }
                }
            }

        }

        private string[] m_cachedCardinalName = new string[10];

        #region Upadate Data
        protected override BasicRenderInformation GetMeshCustom1(ushort id, int boardIdx, int kilometers, BoardDescriptorMileageMarkerXml descriptor)
        {
            byte direction = (boardIdx == 1 ? m_segmentCachedInfo[id].Third : m_segmentCachedInfo[id].Second).cardinalDirection8;
            if (m_cachedCardinalName[direction] == null)
            {
                m_cachedCardinalName[direction] = Locale.Get("K45_CARDINAL_POINT_LONG", direction.ToString()).ToUpper();
            }
            return RenderUtils.GetTextData(m_cachedCardinalName[direction], DrawFont);

        }
        protected override BasicRenderInformation GetMeshCurrentNumber(ushort id, int boardIdx, int kilometers, BoardDescriptorMileageMarkerXml descriptor) => RenderUtils.GetTextData(kilometers.ToString(), DrawFont);

        private PropInfo m_cachedPropInfo;

        protected override BasicRenderInformation GetOwnNameMesh(ushort id, int boardIdx, int secIdx, BoardDescriptorMileageMarkerXml descriptor) => RenderUtils.GetTextData("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW", DrawFont);
        #endregion

        public override Color? GetColor(ushort buildingID, int idx, int secIdx, BoardDescriptorMileageMarkerXml descriptor) => descriptor.PropColor;

        protected override InstanceID GetPropRenderID(ushort nodeId)
        {
            InstanceID result = default;
            result.NetNode = nodeId;
            return result;
        }

        public override Color GetContrastColor(ushort refID, int boardIdx, int secIdx, BoardDescriptorMileageMarkerXml descriptor) => KlyteMonoUtils.ContrastColor(descriptor.PropColor);
        internal void CleanDescriptor()
        {
            Data.CurrentDescriptor = new BoardDescriptorMileageMarkerXml();
            SoftReset();
        }

    }
}
