using PCB;

using System;

namespace EasyEDA_Loader
{
    internal class EEPCB
    {
        public class LayerMapException : Exception
        {
            public LayerMapException(string message) : base(message)
            {

            }
        }
        public static IPCB_LibComponent CreateFootprintInLib(string name, string description)
        {
            var pcbLib = AltiumApi.GlobalVars.PCBServer.GetCurrentPCBLibrary();
            if (pcbLib == null) return null;
            var footprint = pcbLib.CreateNewComponent();
            pcbLib.SetState_CurrentComponent(footprint);
            var uid = pcbLib.GetUniqueCompName(name);
            footprint.SetState_Pattern(uid);
            footprint.SetState_Description(description);
            AltiumApi.GlobalVars.PCBServer.PostProcess();
            return footprint;
        }

        public static void AddToPCB(IPCB_LibComponent c, object obj)
        {
            c.GetState_Board().AddPCBObject(obj);
            c.AddPCBObject(obj);
        }

        public static TLayerConstant EELayerToAltium(string layer)
        {
            switch (layer)
            {
                case "TopLayer": return TLayerConstant.eTopLayer;
                case "BottomLayer": return TLayerConstant.eBottomLayer;
                case "TopSilkLayer": return TLayerConstant.eTopOverlay;
                case "BottomSilkLayer": return TLayerConstant.eBottomOverlay;
                case "TopPasteMaskLayer": return TLayerConstant.eTopPaste;
                case "BottomPasteMaskLayer": return TLayerConstant.eBottomPaste;
                case "TopSolderMaskLayer": return TLayerConstant.eTopSolder;
                case "BottomSolderMaskLayer": return TLayerConstant.eBottomPaste;
                case "BoardOutline": return TLayerConstant.eMechanical1;
                case "Multi-Layer": return TLayerConstant.eMultiLayer;
                case "TopAssembly": return TLayerConstant.eMechanical7;
                case "Mechanical": return TLayerConstant.eMechanical15;
                case "3DModel": return TLayerConstant.eMechanical13;
                default: throw new LayerMapException($"Invalid layer {layer}");
            }
        }

        public static IPCB_Track CreateLine(IPCB_LibComponent c, TLayerConstant layer, double x1, double y1, double x2, double y2, double width)
        {
            var track = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.eTrackObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_Track;
            if (track == null) return null;
            track.SetState_Width(AltiumApi.MmToCoord(width));
            track.SetState_V7Layer(new V7_Layer(layer));
            track.SetState_X1(AltiumApi.MmToCoord(x1) + c.GetState_XLocation());
            track.SetState_X2(AltiumApi.MmToCoord(x2) + c.GetState_XLocation());
            track.SetState_Y1(AltiumApi.MmToCoord(y1) + c.GetState_YLocation());
            track.SetState_Y2(AltiumApi.MmToCoord(y2) + c.GetState_YLocation());
            return track;
        }

        public static IPCB_Arc CreateArc(IPCB_LibComponent c, TLayerConstant layer, double x, double y, double rad, double width, double startAngle, double endAngle)
        {
            var circle = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.eArcObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_Arc;
            if (circle == null) return null;
            circle.SetState_CenterX(AltiumApi.MmToCoord(x) + c.GetState_XLocation());
            circle.SetState_CenterY(AltiumApi.MmToCoord(y) + c.GetState_YLocation());
            circle.SetState_Radius(AltiumApi.MmToCoord(rad));
            circle.SetState_LineWidth(AltiumApi.MmToCoord(width));
            circle.SetState_StartAngle(startAngle);
            circle.SetState_EndAngle(endAngle);
            circle.SetState_V7Layer(new V7_Layer(layer));
            return circle;
        }

        public static IPCB_Pad4 CreatePTH(IPCB_LibComponent c, TLayerConstant layer, TExtendedHoleType holeType, TShape padShape, double x, double y, double height, double width, double holeSize, string name, bool plated, double rotation)
        {
            var pth = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.ePadObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_Pad4;
            if (pth == null) return null;
            pth.SetState_Mode(TPadMode.ePadMode_Simple);
            pth.SetState_Name(name);
            pth.SetState_HoleType(holeType);
            pth.SetState_HoleSize(AltiumApi.MmToCoord(holeSize));
            pth.SetState_Rotation(rotation);
            pth.SetState_Plated(plated);
            pth.SetState_TopShape(padShape);
            pth.SetState_TopXSize(AltiumApi.MmToCoord(width));
            pth.SetState_TopYSize(AltiumApi.MmToCoord(height));
            pth.SetState_V7Layer(new V7_Layer(layer));
            pth.SetState_XLocation(AltiumApi.MmToCoord(x) + c.GetState_XLocation());
            pth.SetState_YLocation(AltiumApi.MmToCoord(y) + c.GetState_YLocation());
            return pth;
        }

        public static IPCB_Via CreateVia(IPCB_LibComponent c, TLayerConstant layerStart, TLayerConstant layerEnd, double x, double y, double size, double holeSize)
        {
            var via = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.eViaObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_Via;
            if (via == null) return null;
            via.SetState_HighLayer(new V7_Layer(layerStart));
            via.SetState_LowLayer(new V7_Layer(layerEnd));
            via.SetState_XLocation(AltiumApi.MmToCoord(x) + c.GetState_XLocation());
            via.SetState_YLocation(AltiumApi.MmToCoord(y) + c.GetState_YLocation());
            via.SetState_HoleSize(AltiumApi.MmToCoord(holeSize));
            via.SetState_Size(AltiumApi.MmToCoord(size));
            return via;
        }

        public static IPCB_Text3 CreateText(IPCB_LibComponent c, TLayerConstant layer, string text, double x, double y, double width, double size, double rotation)
        {
            var textObject = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.eTextObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_Text3;
            if (textObject == null) return null;
            textObject.SetState_V7Layer(new V7_Layer(layer));
            textObject.SetState_XLocation(AltiumApi.MmToCoord(x) + c.GetState_XLocation());
            textObject.SetState_YLocation(AltiumApi.MmToCoord(y) + c.GetState_XLocation());
            textObject.SetState_Text(text);
            textObject.SetState_Size(AltiumApi.MmToCoord(size));
            textObject.SetState_Width(AltiumApi.MmToCoord(width));
            textObject.SetState_Rotation(rotation);
            return textObject;
        }

        public static IPCB_ComponentBody CreateComponentBody(IPCB_LibComponent c, string fileName, double rx, double ry, double rz, double x, double y, double z)
        {
            var stepModel = AltiumApi.GlobalVars.PCBServer.PCBObjectFactory(TObjectId.eComponentBodyObject, TDimensionKind.eNoDimension, TObjectCreationMode.eCreate_Default) as IPCB_ComponentBody;
            if (stepModel == null) return null;
            var model = stepModel.ModelFactory_FromFilename(fileName, false);
            if (model == null) return null;
            model.SetState(rx, ry, rz, AltiumApi.MmToCoord(z));
            stepModel.SetModel(model);
            // Model is created at the bottom-left origin of the board, so we need to offset it
            stepModel.MoveByXY(AltiumApi.MmToCoord(x) + c.GetState_Board().GetState_XOrigin(), AltiumApi.MmToCoord(y) + c.GetState_Board().GetState_YOrigin());
            return stepModel;
        }
    }
}
