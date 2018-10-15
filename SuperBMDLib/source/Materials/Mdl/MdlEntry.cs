﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameFormatReader.Common;
using SuperBMDLib.Materials.Enums;

namespace SuperBMDLib.Materials.Mdl
{
    public class MdlEntry
    {
        string Name;
        List<BPCommand> BPCommands;
        List<XFCommand> XFCommands;

        public MdlEntry()
        {
            BPCommands = new List<BPCommand>();
            XFCommands = new List<XFCommand>();
        }

        public MdlEntry(Material mat, List<BinaryTextureImage> textures)
        {
            Name = mat.Name;
            BPCommands = new List<BPCommand>();
            XFCommands = new List<XFCommand>();

            GenerateTextureCommands(mat, textures);
            GenerateTexGenCommands(mat, textures);
            GenerateTevStageCommands(mat);
            GenerateTevColorCommands(mat);
            GenerateKonstColorCommands(mat);
            GenerateTevColorAndAlphaCommands(mat);
            GenerateTevKonstSelAndSwapModeCommands(mat);
            GenerateRas1IrefAndIndImaskCommands(mat);
            GenerateAlphaCompareCommand(mat);
            GenerateBlendModeCommands(mat);
            GenerateZModeCommand(mat);
            GenerateZCompLocCommands(mat);
            GenerateCullModeCommands(mat);
        }

        private void GenerateTextureCommands(Material mat, List<BinaryTextureImage> textures)
        {
            BPRegister[] texInfoRegs = new BPRegister[] { BPRegister.TX_SETIMAGE0_I0, BPRegister.TX_SETIMAGE0_I1, BPRegister.TX_SETIMAGE0_I2,
                                                          BPRegister.TX_SETIMAGE0_I3, BPRegister.TX_SETIMAGE0_I4, BPRegister.TX_SETIMAGE0_I5,
                                                          BPRegister.TX_SETIMAGE0_I6, BPRegister.TX_SETIMAGE0_I7 };

            BPRegister[] texIndexRegs = new BPRegister[] { BPRegister.TX_SETIMAGE3_I0, BPRegister.TX_SETIMAGE3_I1, BPRegister.TX_SETIMAGE3_I2,
                                                           BPRegister.TX_SETIMAGE3_I3, BPRegister.TX_SETIMAGE3_I4, BPRegister.TX_SETIMAGE3_I5,
                                                           BPRegister.TX_SETIMAGE3_I6, BPRegister.TX_SETIMAGE3_I7 };

            BPRegister[] texMode0Regs = new BPRegister[] { BPRegister.TX_SET_MODE0_I0, BPRegister.TX_SET_MODE0_I1, BPRegister.TX_SET_MODE0_I2,
                                                           BPRegister.TX_SET_MODE0_I3, BPRegister.TX_SET_MODE0_I4, BPRegister.TX_SET_MODE0_I5,
                                                           BPRegister.TX_SET_MODE0_I6, BPRegister.TX_SET_MODE0_I7 };

            BPRegister[] texMode1Regs = new BPRegister[] { BPRegister.TX_SET_MODE1_I0, BPRegister.TX_SET_MODE1_I1, BPRegister.TX_SET_MODE1_I2,
                                                           BPRegister.TX_SET_MODE1_I3, BPRegister.TX_SET_MODE1_I4, BPRegister.TX_SET_MODE1_I5,
                                                           BPRegister.TX_SET_MODE1_I6, BPRegister.TX_SET_MODE1_I7 };

            for (int i = 0; i < 8; i++)
            {
                if (mat.TextureIndices[i] == -1)
                    continue;

                BinaryTextureImage curTex = textures[mat.TextureIndices[i]];

                // Records width, height, and format
                BPCommand texInfo = new BPCommand() { Register = texInfoRegs[i] };
                texInfo.SetBits(curTex.Width - 1, 0, 10);
                texInfo.SetBits(curTex.Height - 1, 10, 10);
                texInfo.SetBits((int)curTex.Format, 20, 4);

                // Records the index of the texture
                BPCommand texIndex = new BPCommand() { Register = texIndexRegs[i] };
                texIndex.SetBits(mat.TextureIndices[i], 0, 24);

                BPCommand mode0 = new BPCommand() { Register = texMode0Regs[i] };
                mode0.SetBits((int)curTex.WrapS, 0, 2);
                mode0.SetBits((int)curTex.WrapT, 2, 2);
                mode0.SetBits((int)curTex.MagFilter, 4, 1);
                mode0.SetBits(4, 5, 3); // TODO curTex.MinFilter
                mode0.SetFlag(true, 8); // Edge LOD
                // LOADBias not supported in bmd2bdl
                mode0.SetBits(0, 19, 2); // Max aniso
                mode0.SetFlag(false, 21); // Bias Clamp TODO
                // MinLOD
                // MaxLOD

                BPCommand mode1 = new BPCommand() { Register = texMode1Regs[i] };

                BPCommands.Add(texIndex);
                BPCommands.Add(texInfo);
                BPCommands.Add(mode0);
                BPCommands.Add(mode1);
            }
        }

        private void GenerateTexGenCommands(Material mat, List<BinaryTextureImage> textures)
        {
            for (int i = 0; i < 8; i++)
            {
                if (mat.TexCoord1Gens[i] == null)
                    continue;

                BPCommand suSizeMask = new BPCommand() { Register = BPRegister.BP_MASK };
                suSizeMask.SetBits(0x03FFFF, 0, 24);

                BPCommand sSize = new BPCommand() { Register = BPRegister.SU_SSIZE0 + (i * 2) };
                sSize.SetFlag(false, 16);
                BPCommand tSize = new BPCommand() { Register = BPRegister.SU_TSIZE0 + (i * 2) };
                tSize.SetFlag(false, 16);
                
                BinaryTextureImage curTex = textures[mat.TextureIndices[i]];
                sSize.SetBits(curTex.Width - 1, 0, 16);
                tSize.SetBits(curTex.Height - 1, 0, 16);

                BPCommands.Add(suSizeMask);
                BPCommands.Add(sSize);
                BPCommands.Add(tSize);
            }
        }

        private void GenerateTevStageCommands(Material mat)
        {
            Dictionary<int, BPCommand> ras1_trefCommandByIndex = new Dictionary<int, BPCommand>();

            for (int i = 0; i < 8; i++)
            {
                BPCommand ras1_tref = new BPCommand() { Register = BPRegister.RAS1_TREF0 + i };

                bool eitherTevOrderExists = false;
                for (int j = 0; j < 2; j++)
                {
                    int tevOrderIndex = (i * 2) + j;
                    if (mat.TevOrders[tevOrderIndex] == null)
                        continue;
                    TevOrder order = mat.TevOrders[tevOrderIndex].Value;
                    eitherTevOrderExists = true;

                    int bitOffset = j * 12;

                    if (order.TexCoord != TexCoordId.Null)
                    {
                        ras1_tref.SetBits((int)order.TexMap, bitOffset + 0, 3);
                        ras1_tref.SetBits((int)order.TexCoord, bitOffset + 3, 3);
                        ras1_tref.SetBits(1, bitOffset + 6, 1);
                    }
                    
                    switch (order.ChannelId)
                    {
                        case GXColorChannelId.Color1:
                        case GXColorChannelId.Alpha1:
                        case GXColorChannelId.Color1A1:
                            ras1_tref.SetBits(1, bitOffset + 7, 3);
                            break;
                        case GXColorChannelId.ColorZero:
                            ras1_tref.SetBits(7, bitOffset + 7, 3);
                            break;
                        case GXColorChannelId.AlphaBump:
                            ras1_tref.SetBits(5, bitOffset + 7, 3);
                            break;
                        case GXColorChannelId.AlphaBumpN:
                            ras1_tref.SetBits(6, bitOffset + 7, 3);
                            break;
                        default:
                            ras1_tref.SetBits(0, bitOffset + 7, 3);
                            break;
                    }
                }

                if (eitherTevOrderExists)
                {
                    BPCommands.Add(ras1_tref);
                }
            }
        }

        private void GenerateTevColorCommands(Material mat)
        {
            for (int i = 1; i < 4; i++)
            {
                int colorIndex = i - 1;
                if (mat.TevColors[colorIndex] == null)
                    continue;
                Util.Color color = mat.TevColors[colorIndex].Value;

                BPCommand tev_colorl = new BPCommand() { Register = BPRegister.TEV_REGISTERL_0 + i*2 };
                BPCommand tev_colorh = new BPCommand() { Register = BPRegister.TEV_REGISTERH_0 + i*2 };

                tev_colorl.SetBits((short)(color.R*255), 0, 11);
                tev_colorl.SetBits((short)(color.A*255), 12, 11);
                tev_colorh.SetBits((short)(color.B*255), 0, 11);
                tev_colorh.SetBits((short)(color.G*255), 12, 11);

                BPCommands.Add(tev_colorl);
                BPCommands.Add(tev_colorh);
            }
        }

        private void GenerateKonstColorCommands(Material mat)
        {
            for (int i = 0; i < 4; i++)
            {
                if (mat.KonstColors[i] == null)
                    continue;
                Util.Color color = mat.KonstColors[i].Value;

                BPCommand tev_kolorl = new BPCommand() { Register = BPRegister.TEV_REGISTERL_0 + i * 2 };
                BPCommand tev_kolorh = new BPCommand() { Register = BPRegister.TEV_REGISTERH_0 + i * 2 };

                // Set flags to indicate that this is konst color.
                tev_kolorl.SetFlag(true, 23);
                tev_kolorh.SetFlag(true, 23);

                tev_kolorl.SetBits((byte)(color.R * 255), 0, 11);
                tev_kolorl.SetBits((byte)(color.A * 255), 12, 11);
                tev_kolorh.SetBits((byte)(color.B * 255), 0, 11);
                tev_kolorh.SetBits((byte)(color.G * 255), 12, 11);

                BPCommands.Add(tev_kolorl);
                BPCommands.Add(tev_kolorh);
            }
        }

        private void GenerateTevColorAndAlphaCommands(Material mat)
        {
            for (int i = 0; i < 16; i++)
            {
                if (mat.TevStages[i] == null)
                    continue;
                TevStage stage = mat.TevStages[i].Value;
                if (mat.SwapModes[i] == null)
                    continue;
                TevSwapMode swapMode = mat.SwapModes[i].Value;

                BPCommand tev_color_env = new BPCommand() { Register = BPRegister.TEV_COLOR_ENV_0 + i * 2 };
                BPCommand tev_alpha_env = new BPCommand() { Register = BPRegister.TEV_ALPHA_ENV_0 + i * 2 };
                BPCommand ind_cmd = new BPCommand() { Register = BPRegister.IND_CMD0 + i };

                tev_color_env.SetBits((int)stage.ColorInD, 0, 4);
                tev_color_env.SetBits((int)stage.ColorInC, 4, 4);
                tev_color_env.SetBits((int)stage.ColorInB, 8, 4);
                tev_color_env.SetBits((int)stage.ColorInA, 12, 4);

                tev_color_env.SetBits((int)stage.ColorBias, 16, 2);
                tev_color_env.SetBits((int)stage.ColorOp, 18, 1);
                tev_color_env.SetFlag(stage.ColorClamp, 19);
                tev_color_env.SetBits((int)stage.ColorScale, 20, 2);
                tev_color_env.SetBits((int)stage.ColorRegId, 22, 2);

                tev_alpha_env.SetBits((int)stage.AlphaInA, 13, 3);
                tev_alpha_env.SetBits((int)stage.AlphaInB, 10, 3);
                tev_alpha_env.SetBits((int)stage.AlphaInC, 7, 3);
                tev_alpha_env.SetBits((int)stage.AlphaInD, 4, 3);

                tev_alpha_env.SetBits((int)stage.AlphaBias, 16, 2);
                tev_alpha_env.SetBits((int)stage.AlphaOp, 18, 1);
                tev_alpha_env.SetFlag(stage.AlphaClamp, 19);
                tev_alpha_env.SetBits((int)stage.AlphaScale, 20, 2);
                tev_alpha_env.SetBits((int)stage.AlphaRegId, 22, 2);

                tev_alpha_env.SetBits(swapMode.RasSel, 0, 2);
                tev_alpha_env.SetBits(swapMode.TexSel, 2, 2);

                BPCommands.Add(tev_color_env);
                BPCommands.Add(tev_alpha_env);
                BPCommands.Add(ind_cmd);
            }
        }

        private void GenerateTevKonstSelAndSwapModeCommands(Material mat)
        {
            for (int i = 0; i < 8; i++)
            {
                BPCommand tev_ksel = new BPCommand() { Register = BPRegister.TEV_KSEL_0 + i };

                for (int j = 0; j < 2; j++)
                {
                    int colorSelIndex = (i * 2) + j;
                    KonstColorSel colorSel = mat.ColorSels[colorSelIndex];
                    KonstAlphaSel alphaSel = mat.AlphaSels[colorSelIndex];

                    int bitOffset = j * 10;

                    tev_ksel.SetBits((int)colorSel, bitOffset + 4, 5);
                    tev_ksel.SetBits((int)alphaSel, bitOffset + 9, 5);
                }

                int swapTableIndex = (i >> 1);
                if (mat.SwapTables[swapTableIndex] != null)
                {
                    TevSwapModeTable swapTable = mat.SwapTables[swapTableIndex].Value;
                    if (i % 2 == 0)
                    {
                        tev_ksel.SetBits(swapTable.R, 0, 2);
                        tev_ksel.SetBits(swapTable.G, 2, 2);
                    }
                    else
                    {
                        tev_ksel.SetBits(swapTable.B, 0, 2);
                        tev_ksel.SetBits(swapTable.A, 2, 2);
                    }
                }

                BPCommands.Add(tev_ksel);
            }
        }

        private void GenerateRas1IrefAndIndImaskCommands(Material mat)
        {
            BPCommand ras1_iref = new BPCommand() { Register = BPRegister.RAS1_IREF };
            ras1_iref.SetBits(0xFFFFFF, 0, 24);

            BPCommand ind_imask = new BPCommand() { Register = BPRegister.IND_IMASK };

            BPCommands.Add(ras1_iref);
            BPCommands.Add(ind_imask);
        }

        private void GenerateAlphaCompareCommand(Material mat)
        {
            BPCommand tev_alphafunc = new BPCommand() { Register = BPRegister.TEV_ALPHAFUNC };

            tev_alphafunc.SetBits(mat.AlphCompare.Reference0, 0, 8);
            tev_alphafunc.SetBits(mat.AlphCompare.Reference1, 8, 8);
            tev_alphafunc.SetBits((int)mat.AlphCompare.Comp0, 16, 3);
            tev_alphafunc.SetBits((int)mat.AlphCompare.Comp1, 19, 3);
            tev_alphafunc.SetBits((int)mat.AlphCompare.Operation, 22, 2);

            BPCommands.Add(tev_alphafunc);
        }

        private void GenerateBlendModeCommands(Material mat)
        {
            BPCommand pe_cmode0_mask = new BPCommand() { Register = BPRegister.BP_MASK };
            BPCommand pe_cmode0 = new BPCommand() { Register = BPRegister.PE_CMODE0 };

            pe_cmode0_mask.SetBits(0x00FFE7, 0, 24);

            switch (mat.BMode.Type)
            {
                case Enums.BlendMode.Blend:
                    pe_cmode0.SetFlag(true, 0);
                    break;
                case Enums.BlendMode.Subtract:
                    pe_cmode0.SetFlag(true, 0);
                    pe_cmode0.SetFlag(true, 11);
                    break;
                case Enums.BlendMode.Logic:
                    pe_cmode0.SetFlag(true, 1);
                    break;
            }

            pe_cmode0.SetBits((int)mat.BMode.Operation, 12, 4);
            pe_cmode0.SetBits((int)mat.BMode.DestinationFact, 5, 3);
            pe_cmode0.SetBits((int)mat.BMode.SourceFact, 8, 3);

            pe_cmode0.SetFlag(false, 2); // TODO: mat.Dither

            BPCommands.Add(pe_cmode0_mask);
            BPCommands.Add(pe_cmode0);
        }

        private void GenerateZModeCommand(Material mat)
        {
            BPCommand pe_zmode = new BPCommand() { Register = BPRegister.PE_ZMODE };
            pe_zmode.SetFlag(mat.ZMode.Enable, 0);
            pe_zmode.SetBits((byte)mat.ZMode.Function, 1, 3);
            pe_zmode.SetFlag(mat.ZMode.UpdateEnable, 4);

            BPCommands.Add(pe_zmode);
        }

        private void GenerateZCompLocCommands(Material mat)
        {
            BPCommand pe_control_mask = new BPCommand() { Register = BPRegister.BP_MASK };
            pe_control_mask.SetBits(0x000040, 0, 24);

            BPCommand pe_control = new BPCommand() { Register = BPRegister.PE_CONTROL };
            pe_control.SetFlag(true, 6); // TODO mat.ZCompLoc

            BPCommands.Add(pe_control_mask);
            BPCommands.Add(pe_control);
        }

        private void GenerateCullModeCommands(Material mat)
        {
            BPCommand gen_mode_mask = new BPCommand() { Register = BPRegister.BP_MASK };
            gen_mode_mask.SetBits(0x07FC3F, 0, 24);

            BPCommand gen_mode = new BPCommand() { Register = BPRegister.GEN_MODE };
            gen_mode.SetBits(mat.NumTexGensCount, 0, 4);
            gen_mode.SetBits(mat.ColorChannelControlsCount, 4, 3);
            gen_mode.SetBits(mat.NumTevStagesCount-1, 10, 4);
            if (mat.CullMode == CullMode.Front)
            {
                gen_mode.SetBits(2, 14, 2);
            }
            else if (mat.CullMode == CullMode.Back)
            {
                gen_mode.SetBits(1, 14, 2);
            }
            else
            {
                gen_mode.SetBits(0, 14, 2);
            }

            BPCommands.Add(gen_mode_mask);
            BPCommands.Add(gen_mode);
        }

        public void Write(EndianBinaryWriter writer)
        {
            foreach (BPCommand cmd in BPCommands)
                cmd.Write(writer);
            foreach (XFCommand cmd in XFCommands)
                cmd.Write(writer);
        }
    }
}