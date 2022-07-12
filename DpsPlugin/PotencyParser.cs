

using System.Linq;
using Dalamud.Utility;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.GeneratedSheets;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace DpsPlugin {
    public static class PotencyParser {
        public delegate void LogFn(string line);
        public static LogFn? Log = null;

        enum Tags {
            IfTag = 0x08
        }

        // Thanks https://github.com/xivapi/SaintCoinach/blob/e0cb0856b10bb75af2b4f5073fcd5453c698d3af/SaintCoinach/Text/Expression.cs
        enum Expression {
            GreaterThanOrEqualTo = 0xE0,    // Followed by two variables
            GreaterThan = 0xE1,             // Followed by one variable
            LessThanOrEqualTo = 0xE2,       // Followed by two variables
            LessThan = 0xE3,                // Followed by one variable
            Equal = 0xE4,                   // Followed by two variables
            NotEqual = 0xE5,                // TODO: Probably

            // TODO: I /think/ I got these right.
            IntegerParameter = 0xE8,        // Followed by one variable
            PlayerParameter = 0xE9,         // Followed by one variable
            StringParameter = 0xEA,         // Followed by one variable
            ObjectParameter = 0xEB,         // Followed by one variable
            
            Byte = 0xF0,
            Int16_MinusOne = 0xF1,          // Followed by a Int16 that is one too high
            Int16_1 = 0xF2,                 // Followed by a Int16
            Int16_2 = 0xF4,                 // Followed by a Int16
            Int24_MinusOne = 0xF5,          // Followed by a Int24 that is one too high
            Int24 = 0xF6,                   // Followed by a Int24

            Int24_SafeZero = 0xFA,          // Followed by a Int24, but 0xFF bytes set to 0 instead.
            Int24_Lsh8 = 0xFD,              // Followed by a Int24, and left-shifted by 8 bits
            Int32 = 0xFE,                   // Followed by a Int32

            Decode = 0xFF,                  // Followed by length (inlcuding length) and data
        }

        // thanks https://github.com/xivapi/SaintCoinach/blob/36e9d613f4bcc45b173959eed3f7b5549fd6f540/SaintCoinach/Text/Parameters/PlayerParameters.cs
        enum PlayerParameters {
            ActiveClassJobIndex = 68,
            LevelIndex1 = 69,     // TODO: I have no idea what the difference between these is.
            LevelIndex2 = 72,     // 72 possibly JOB and 69 CLASS ?
            GamePadTypeIndex = 75,   // TODO: 0 for XInput, 1 for PS3, 2 for PS4?
            RegionIndex = 77,       // I think it is, anyway. Only found it for formatting dates. 0-2 = ?; 3 = EU?; 4+ = ?

        }

        public class ParseContext {
            public int Level;
            public int ActiveClassJobIndex;
        }

        public static int FindPotency(Lumina.Excel.ExcelSheet<Action> actionSheet, Lumina.Excel.ExcelSheet<ActionTransient> actionTransientSheet, int actionId, int level) {
            ActionTransient actionTransient = actionTransientSheet.Where(a => a.RowId == actionId).FirstOrDefault()!;
            Action action = actionSheet.Where(a => a.RowId == actionId).FirstOrDefault()!;
            SeString seString = SeStringExtensions.ToDalamudString(actionTransient.Description);
            ParseContext ctx = new ParseContext();
            ctx.Level = level;
            ctx.ActiveClassJobIndex = action.ClassJob.Value!.JobIndex;
            StringBuilder descriptionBuilder = new StringBuilder();
                
            foreach (Payload payload in seString.Payloads) {
                if (payload.Type == PayloadType.RawText) {
                    descriptionBuilder.Append(((Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload)payload).Text);
                } else if (payload.Type == PayloadType.Unknown) {
                    byte[] payloadBytes = ((Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload)payload).Data;
                    uint pos = 0;
                    ReduceExpressionsToString(payloadBytes, descriptionBuilder, ref pos, ctx);
                } else {
                    // Unknown payload type. we don't care.
                    // $"[Payload {payload.Type.ToString()}]";
                }
            };
            int num = -1;
            if (Log != null) {
                Log($"Description: {descriptionBuilder}");
            }
            // int num = int.Parse(Regex.Match(description, @"\d+").ToString());

            return num;
        }

        public static string ReduceExpressionsToString(byte[] data, StringBuilder builder, ref uint pos, ParseContext ctx) {
            byte startByte = data[pos++];
            ParseAssertEqual(startByte, 0x02);
            byte tagType = data[pos++];
            uint length = GetInteger(data, ref pos);
            uint clone = pos;
            ParseTag(tagType, length, data, builder, ref clone, ctx);

            pos += length;
            byte endByte = data[pos++];
            ParseAssertEqual(endByte, 0x03);
            return "";
        }

        public static System.Object ParseRawPayload(byte[] data, ref uint pos, ParseContext ctx) {
            byte startByte = data[pos++];
            ParseAssertEqual(startByte, 0x02);
            byte tagType = data[pos++];
            uint length = GetInteger(data, ref pos);
            uint clone = pos;
            ParseTag(tagType, length, data, ref clone, ctx);

            pos += length;
            byte endByte = data[pos++];
            ParseAssertEqual(endByte, 0x03);
            return "";
        }

        
        public static uint GetInteger(byte[] data, ref uint pos)
        {
            uint marker = data[pos++];
            if (marker < 0xD0)
                return marker - 1;

            // the game adds 0xF0 marker for values >= 0xCF
            // uasge of 0xD0-0xEF is unknown, should we throw here?
            // if (marker < 0xF0) throw new NotSupportedException();

            marker = (marker + 1) & 0b1111;

            var ret = new byte[4];
            for (var i = 3; i >= 0; i--)
            {
                ret[i] = (marker & (1 << i)) == 0 ? (byte)0 : data[pos++];
            }

            return System.BitConverter.ToUInt32(ret, 0);

        }

        public static void ParseTag(byte tagType, uint length, byte[] data, ref uint pos, ParseContext ctx)
        {
            if (tagType == ((byte)Tags.IfTag))
            {
                ParseIfTag(tagType, length, data, ref pos, ctx);
            }
        }

        public static System.Object ParseIfTag(byte tagType, uint length, byte[] data, ref uint pos, ParseContext ctx)
        {
            uint lastByte = pos + length - 1u;
            System.Object result = EvaluateExpression(data, ref pos, ctx);
            System.Object trueResult = EvaluateExpression(data, ref pos, ctx);
            System.Object falseResult = EvaluateExpression(data, ref pos, ctx);
            
            return (System.Boolean)result ? trueResult : falseResult;
        }


        // Thanks https://github.com/xivapi/SaintCoinach/blob/e0cb0856b10bb75af2b4f5073fcd5453c698d3af/SaintCoinach/Text/XivStringDecoder.cs
        public static System.Object EvaluateExpression(byte[] data, ref uint pos, ParseContext ctx) {
            byte expressionType = data[pos++];
            // if (Log != null) {
            //     Log($"Parse pos {pos} expression {((Expression)expressionType).ToString()} ({string.Format("{0:X2}", expressionType)}) bytes: ");
            //     for(int i = 0; i < data.Length; i++) {
            //         Log(string.Format("{0:X2} ", data[i]));
            //     }
            //     Log("\n");

            //     // if (Log != null) {
            //     //     Log($"Expression type: {((Expression)expressionType).ToString()}\n");
            //     // }

            // }

            if (expressionType < 0xD0) {
                byte val = (byte)((int)expressionType - 1);
                return (int)val;
            }

            switch ((Expression)expressionType) {
                case Expression.Decode: {
                    byte redundantLengthLol = data[pos++];
                    return ParseRawPayload(data, ref pos, ctx);
                }
                // case Expression.Byte:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Byte));
                // case Expression.Int16_MinusOne:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int16) - 1);
                // case Expression.Int16_1:
                // case Expression.Int16_2:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int16));
                // case Expression.Int24_MinusOne:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int24) - 1);
                // case Expression.Int24:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int24));
                // case Expression.Int24_Lsh8:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int24) << 8);
                // case Expression.Int24_SafeZero: {
                //         var v16 = input.ReadByte();
                //         var v8 = input.ReadByte();
                //         var v0 = input.ReadByte();

                //         int v = 0;
                //         if (v16 != byte.MaxValue)
                //             v |= v16 << 16;
                //         if (v8 != byte.MaxValue)
                //             v |= v8 << 8;
                //         if (v0 != byte.MaxValue)
                //             v |= v0;

                //         return new Nodes.StaticInteger(v);
                //     }
                // case Expression.Int32:
                //     return new Nodes.StaticInteger(GetInteger(input, IntegerType.Int32));
                // case Expression.GreaterThanOrEqualTo:
                // case Expression.GreaterThan:
                // case Expression.LessThanOrEqualTo:
                // case Expression.LessThan:
                // case Expression.NotEqual:
                case Expression.Equal: {
                    builder.Append("Equal(");
                    /*var left =*/ EvaluateExpression(data, builder, ref pos, ctx);
                    builder.Append(",");
                    /*var right =*/ EvaluateExpression(data, builder, ref pos, ctx);
                    builder.Append(")");
                    // return new Nodes.Comparison(exprType, left, right);
                    break;
                }
                // case Expression.IntegerParameter:
                case Expression.PlayerParameter:
                    byte param = data[pos++];
                    builder.Append($"PlayerParameter({param-1})");
                    break;
                // case Expression.StringParameter:
                // case Expression.ObjectParameter:
                //     return new Nodes.Parameter(exprType, DecodeExpression(input));
                // default:
                //     throw new System.NotSupportedException();
            }
        }

        public static void ParseConditionalOutput(byte[] data, uint lastTagBytePos, StringBuilder builder, ref uint pos, ParseContext ctx) {
            int exprs = 0;
            while (pos < lastTagBytePos) {
                EvaluateExpression(data, builder, ref pos, ctx);
                builder.Append(",");
            }
            EvaluateExpression(data, builder, ref pos, ctx);

            // Only one instance with more than two expressions (LogMessage.en[1115][4])
            // TODO: Not sure how it should be handled, discarding all but first and second for now.
            // if (exprs.Count > 0)
            //     trueValue = exprs[0];
            // else
            //     trueValue = null;

            // if (exprs.Count > 1)
            //     falseValue = exprs[1];
            // else
            //     falseValue = null;

        }

        public static void ParseAssertEqual(byte a, byte b)
        {
            if (a != b)
            {
                throw new System.Exception();
            }
        }

    
    }
}