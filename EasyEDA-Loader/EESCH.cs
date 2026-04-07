using EDP;
using SCH;
using System.Windows.Forms;

namespace EasyEDA_Loader
{
    public class EESCH
    {
        public static ISch_Lib GetCurrentSchLibrary()
        {
            var schDoc = AltiumApi.GlobalVars.SCHServer.GetCurrentSchDocument();
            if (schDoc == null)
            {
                MessageBox.Show("This is not a SCH library document", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return null;
            }
            if (schDoc != null && schDoc.GetState_ObjectId() != SCH.TObjectId.eSchLib)
            {
                MessageBox.Show("Open schematic library", "EasyEDA Loader Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return null;
            }

            return schDoc as ISch_Lib;
        }
        public static ISch_Component CreateComponent(string name, string desc, string designator)
        {
            var schComponent = AltiumApi.GlobalVars.SCHServer.SchObjectFactory(SCH.TObjectId.eSchComponent, SCH.TObjectCreationMode.eCreate_Default) as ISch_Component;
            if (schComponent == null)
                return null;

            schComponent.SetState_CurrentPartID(1);
            schComponent.SetState_DisplayMode(0);
            schComponent.SetState_LibReference(name);
            schComponent.GetState_SchDesignator().SetState_Text(designator);
            schComponent.SetState_ComponentDescription(desc);
            return schComponent;
        }


        public static void AddParameter(ISch_Component c, string name, string value)
        {
            ISch_Parameter param = c.AddSchParameter();
            param.SetState_Text(value);
            param.SetState_Name(name);
            param.SetState_ShowName(false);
            param.SetState_IsHidden(true);
        }

        public static int RGB(int r, int g, int b)
        {
            return (b << 16) | (g << 8) | r;
        }

        public class FontInfo
        {
            public int Size { get; set; }
            public int Rotation { get; set; } = 0;
            public bool Underline { get; set; } = false;
            public bool Italic { get; set; } = false;
            public bool Bold { get; set; } = false;
            public bool Strikout { get; set; } = false;
            public string Name { get; set; }
            public int Color { get; set; } = 0;
        }

        public static ISch_Pin CreatePin(ISch_Lib schLib, ISch_Component c, double x, double y, string designator, string name, TRotationBy90 orientation, double length, TPinElectrical pinType, bool showName, FontInfo fontInfo)
        {
            var schPin = AltiumApi.GlobalVars.SCHServer.SchObjectFactory(SCH.TObjectId.ePin, SCH.TObjectCreationMode.eCreate_Default) as ISch_Pin;
            if (schPin == null)
                return null;

            schPin.SetState_Location(new DXP.Point
            {
                X = AltiumApi.MilsToCoord(x),
                Y = AltiumApi.MilsToCoord(y)
            });
            schPin.SetState_PinLength(AltiumApi.MilsToCoord(length));
            schPin.SetState_Color(0);
            schPin.SetState_Orientation(orientation);
            schPin.SetState_Designator(designator);
            schPin.SetState_Name(name);
            schPin.SetState_Electrical(pinType);
            schPin.SetState_ShowName(showName);
            schPin.SetState_OwnerPartId(schLib.GetState_CurrentSchComponentPartId());
            schPin.SetState_OwnerPartDisplayMode(schLib.GetState_CurrentSchComponentDisplayMode());

            if (fontInfo != null)
            {
                int fontId = AltiumApi.GlobalVars.SCHServer.GetState_FontManager().GetFontID(fontInfo.Size, fontInfo.Rotation, fontInfo.Underline, fontInfo.Italic, fontInfo.Bold, fontInfo.Strikout, fontInfo.Name);
                schPin.SetState_Designator_FontMode(TPinItemMode.ePinItemMode_Custom);
                schPin.SetState_Designator_CustomFontID(fontId);
                schPin.SetState_Designator_CustomColor(fontInfo.Color);
            }

            c.AddSchObject(schPin);
            return schPin;
        }

        public static ISch_Pin CreateLeftPin(ISch_Lib schLib, ISch_Component c, double x, double y, string designator, string name, double length, TPinElectrical pinType, bool showName, FontInfo fontInfo)
        {
            return CreatePin(schLib, c, x, y, designator, name, TRotationBy90.eRotate180, length, pinType, showName, fontInfo);
        }
        public static ISch_Pin CreateRightPin(ISch_Lib schLib, ISch_Component c, double x, double y, string designator, string name, double length, TPinElectrical pinType, bool showName, FontInfo fontInfo)
        {
            return CreatePin(schLib, c, x, y, designator, name, TRotationBy90.eRotate0, length, pinType, showName, fontInfo);
        }
        public static ISch_Pin CreateTopPin(ISch_Lib schLib, ISch_Component c, double x, double y, string designator, string name, double length, TPinElectrical pinType, bool showName, FontInfo fontInfo)
        {
            return CreatePin(schLib, c, x, y, designator, name, TRotationBy90.eRotate90, length, pinType, showName, fontInfo);
        }
        public static ISch_Pin CreateBottomPin(ISch_Lib schLib, ISch_Component c, double x, double y, string designator, string name, double length, TPinElectrical pinType, bool showName, FontInfo fontInfo)
        {
            return CreatePin(schLib, c, x, y, designator, name, TRotationBy90.eRotate270, length, pinType, showName, fontInfo);
        }

        public static void CreateRectangle(ISch_Lib schLib, ISch_Component c, double x1, double y1, double x2, double y2)
        {
            var rect = AltiumApi.GlobalVars.SCHServer.SchObjectFactory(SCH.TObjectId.eRectangle, SCH.TObjectCreationMode.eCreate_Default) as ISch_Rectangle;
            if (rect == null)
                return;
            rect.SetState_LineWidth(TSize.eSmall);
            rect.SetState_Location(new DXP.Point
            {
                X = AltiumApi.MilsToCoord(x1),
                Y = AltiumApi.MilsToCoord(y1)
            });
            rect.SetState_Corner(new DXP.Point
            {
                X = AltiumApi.MilsToCoord(x2),
                Y = AltiumApi.MilsToCoord(y2)
            });

            rect.SetState_Color(RGB(128, 0, 0));
            rect.SetState_AreaColor(RGB(255, 255, 176));
            rect.SetState_IsSolid(true);
            rect.SetState_OwnerPartId(schLib.GetState_CurrentSchComponentPartId());
            rect.SetState_OwnerPartDisplayMode(schLib.GetState_CurrentSchComponentDisplayMode());
            c.AddSchObject(rect);
        }

        public static void CreateLine(ISch_Lib schLib, ISch_Component c, double x1, double y1, double x2, double y2)
        {
            var line = AltiumApi.GlobalVars.SCHServer.SchObjectFactory(SCH.TObjectId.eLine, SCH.TObjectCreationMode.eCreate_Default) as ISch_Line;
            if (line == null)
                return;

            line.SetState_LineWidth(TSize.eSmall);
            line.SetState_Location(new DXP.Point
            {
                X = AltiumApi.MilsToCoord(x1),
                Y = AltiumApi.MilsToCoord(y1)
            });
            line.SetState_Corner(new DXP.Point
            {
                X = AltiumApi.MilsToCoord(x2),
                Y = AltiumApi.MilsToCoord(y2)
            });
            line.SetState_Color(0);
            line.SetState_OwnerPartId(schLib.GetState_CurrentSchComponentPartId());
            line.SetState_OwnerPartDisplayMode(schLib.GetState_CurrentSchComponentDisplayMode());
            c.AddSchObject(line);
        }

        public static void AssignFootprint(ISch_Component c, string libraryPath, string modelName, string modelMapping)
        {
            var modelType = "PCBLIB";
            var model = c.AddSchImplementation();
            model.ClearAllDatafileLinks();
            model.SetState_MapAsString(modelMapping);
            model.SetState_ModelName(modelName);
            model.SetState_ModelType(modelType);
            model.AddDataFileLink(modelName, libraryPath, modelType);
            model.SetState_IsCurrent(true);
        }

    }
}
