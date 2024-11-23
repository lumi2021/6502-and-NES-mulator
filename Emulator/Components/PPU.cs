using Emulator.Components.Core;
using ImGuiNET;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Emulator.Components;

public class PPU : Component
{
    private byte[,,] _vram_chr = new byte[16 * 32, 8, 8];
    private byte[] _vram_nametable = new byte[0x0800];
    private byte[] _vram_atbttable = new byte[0x0020];
    private byte[] _vram_oam = new byte[256];

    private byte[] _video_out_buffer = new byte[256 * 240 * 3];
    private uint _video_out_texture = 0;

    public bool vBlankNMInterrupt = false;
    public bool isMaster = false;
    public byte spriteHeight = 8;
    public byte backgroundPatternTable = 0;
    public byte spritePatternTable = 0;
    public byte incrementPerRead = 1;

    private byte _ppumask = 0;
    private byte _ppustat = 0;
    private byte _oamaddr = 0;
    private byte _oamdata = 0;

    private ushort _currVRAMAddr = 0;
    private ushort _tempVRAMAddr = 0;

    private byte _scrollX = 0;
    private byte _scrollY = 0;

    private byte _oamdma = 0;

    private bool _wLatch = false;


    #region stat flags
    public bool OnVblank
    {
        get => (_ppustat & 0b_1000_0000) != 0;
        set => _ppustat = (byte)((_ppustat & ~0b_1000_0000) | (value ? 0b_1000_0000 : 0));
    }
    #endregion

    #region SilkNet shit
    private int _texWrapMode = (int)TextureWrapMode.Repeat;
    private int _texMinFilter = (int)TextureMinFilter.Nearest;
    private int _texMagFilter = (int)TextureMagFilter.Nearest;
    #endregion

    private (byte r, byte g, byte b)[] palletes = [
        (0x80, 0x80, 0x80), (0x00, 0x3D, 0xA6), (0x00, 0x12, 0xB0), (0x44, 0x00, 0x96), (0xA1, 0x00, 0x5E),
        (0xC7, 0x00, 0x28), (0xBA, 0x06, 0x00), (0x8C, 0x17, 0x00), (0x5C, 0x2F, 0x00), (0x10, 0x45, 0x00),
        (0x05, 0x4A, 0x00), (0x00, 0x47, 0x2E), (0x00, 0x41, 0x66), (0x00, 0x00, 0x00), (0x05, 0x05, 0x05),
        (0x05, 0x05, 0x05), (0xC7, 0xC7, 0xC7), (0x00, 0x77, 0xFF), (0x21, 0x55, 0xFF), (0x82, 0x37, 0xFA),
        (0xEB, 0x2F, 0xB5), (0xFF, 0x29, 0x50), (0xFF, 0x22, 0x00), (0xD6, 0x32, 0x00), (0xC4, 0x62, 0x00),
        (0x35, 0x80, 0x00), (0x05, 0x8F, 0x00), (0x00, 0x8A, 0x55), (0x00, 0x99, 0xCC), (0x21, 0x21, 0x21),
        (0x09, 0x09, 0x09), (0x09, 0x09, 0x09), (0xFF, 0xFF, 0xFF), (0x0F, 0xD7, 0xFF), (0x69, 0xA2, 0xFF),
        (0xD4, 0x80, 0xFF), (0xFF, 0x45, 0xF3), (0xFF, 0x61, 0x8B), (0xFF, 0x88, 0x33), (0xFF, 0x9C, 0x12),
        (0xFA, 0xBC, 0x20), (0x9F, 0xE3, 0x0E), (0x2B, 0xF0, 0x35), (0x0C, 0xF0, 0xA4), (0x05, 0xFB, 0xFF),
        (0x5E, 0x5E, 0x5E), (0x0D, 0x0D, 0x0D), (0x0D, 0x0D, 0x0D), (0xFF, 0xFF, 0xFF), (0xA6, 0xFC, 0xFF),
        (0xB3, 0xEC, 0xFF), (0xDA, 0xAB, 0xEB), (0xFF, 0xA8, 0xF9), (0xFF, 0xAB, 0xB3), (0xFF, 0xD2, 0xB0),
        (0xFF, 0xEF, 0xA6), (0xFF, 0xF7, 0x9C), (0xD7, 0xE8, 0x95), (0xA6, 0xED, 0xAF), (0xA2, 0xF2, 0xDA),
        (0x99, 0xFF, 0xFC), (0xDD, 0xDD, 0xDD), (0x11, 0x11, 0x11), (0x11, 0x11, 0x11)
    ];

    private void WritePPUCtrl(byte value)
    {
        vBlankNMInterrupt = (value & 0b_1000_0000) != 0;
        isMaster = (value & 0b_0100_0000) == 0;
        spriteHeight = (byte)(((value & 0b_0010_0000) == 0) ? 8 : 16);
        backgroundPatternTable = (byte)(((value & 0b_0001_0000) == 0) ? 0 : 1);
        spritePatternTable = (byte)(((value & 0b_0000_1000) == 0) ? 0 : 1);
        incrementPerRead = (byte)(((value & 0b_0000_0100) == 0) ? 1 : 32);

        _tempVRAMAddr = (ushort)((_tempVRAMAddr & 0b_111_00_11_11111111) | ((value & 0b_0000_0011) << 11));
    }
    private void WritePPUScroll(byte value)
    {
        if (!_wLatch)
        {
            _tempVRAMAddr = (ushort)((_tempVRAMAddr & 0b_11111111_11100000) | (value >> 3));
            _scrollX = value; 
        }
        else
        {
            _tempVRAMAddr = (ushort)((_tempVRAMAddr * 0b_000_11_00000_11111) | ((value & 0b_00000_111) << 12) | ((value & 0b_11111_000) << 5));
            _scrollY = value;
        }

        _wLatch = !_wLatch;
    }
    private void WritePPUAddress(byte value)
    {
        if (!_wLatch)
        {
            _tempVRAMAddr = (ushort)((_tempVRAMAddr & 0b_00_000000_11111111) | ((value & 0b_00_111111) << 8));
        }
        else
        {
            _tempVRAMAddr = (ushort)((_tempVRAMAddr & 0b_00_111111_00000000) | value);
            _currVRAMAddr = _tempVRAMAddr;
        }

        _wLatch = !_wLatch;
    }

    private void WritePPUData(byte value)
    {
        ushort addr = _currVRAMAddr;

        if (addr >= 0x2000 && addr < 0x3000)
        {
            addr = ProcessNametableMirroring(addr);
            _vram_nametable[addr - 0x2000] = value;
        }

        else if (addr >= 0x3F00 && addr <= 0x3F1F)
            _vram_atbttable[addr - 0x3F00] = value;
        else if (addr >= 0x3F20 && addr <= 0x3FFF)
            _vram_atbttable[(addr - 0x3F00) % 0x20] = value;

        _currVRAMAddr += incrementPerRead;
        _updateNametablesSheet = true;
    }
    private byte ReadPPUData(ushort addr)
    {
        if (addr < 0x2000) return Read(addr);

        if (addr >= 0x2000 && addr < 0x3000)
        {
            var gAddr = ProcessNametableMirroring(addr);

            return _vram_nametable[gAddr - 0x2000];
        }

        else if (addr >= 0x3F00 && addr < 0x3F20)
            return _vram_atbttable[addr - 0x3F00];
        else if (addr >= 0x3F20 && addr <= 0x3FFF)
            return _vram_atbttable[(addr - 0x3F00) % 0x20];

        Console.WriteLine($"Trying to read PPUDATA ${addr:X4} (invalid range)");
        return 0;
    }
    private byte[] ReadPPUDataRange(ushort addr, int length)
    {
        byte[] a = new byte[length];

        for (int i = 0; i < length; i++) a[i] = ReadPPUData((ushort)(addr + i));

        return a;
    }

    private ushort ProcessNametableMirroring(ushort addr)
    {
        int gAddr = addr;

        // solve odd mirroring addresses
        //if (gAddr < 0x2000) gAddr += 0x2000;

        // +---+---+
        // | 0 | 1 | 0x2000 0x2400
        // +---+---+
        // | 2 | 3 | 0x2800 0x2C00
        // +---+---+
        // 0x3F00 - 0x3F1F | 0x3F20 .. 0x3FFF

        if (gAddr >= 0x2000 && gAddr <= 0x3000)
        {
            int nametableIndex = 0;

            if (gAddr < 0x2400)
            {
                nametableIndex = 0;
                gAddr -= 0x2000;
            }
            else if (gAddr < 0x2800)
            {
                nametableIndex = 1;
                gAddr -= 0x2400;
            }
            else if (gAddr < 0x2C00)
            {
                nametableIndex = 2;
                gAddr -= 0x2800;
            }
            else if (gAddr < 0x3000)
            {
                nametableIndex = 3;
                gAddr -= 0x2C00;
            }

            var mirroring = system.Rom.RomData.NametableArrangement;

            if (mirroring == NametableArrangement.Vertical)
            {
                if (nametableIndex == 0 || nametableIndex == 2) gAddr += 0x2000;
                if (nametableIndex == 1 || nametableIndex == 3) gAddr += 0x2400;
            }
            else
            {
                if (nametableIndex == 0 || nametableIndex == 1) gAddr += 0x2000;
                if (nametableIndex == 2 || nametableIndex == 3) gAddr += 0x2400;
            }
        }

        return (ushort)gAddr;
    }

    private void CopyOAMData(byte HAddr)
    {
        _vram_oam = system.Ram.CopyPage(HAddr);
    }

    public PPU(VirtualSystem mb) : base(mb)
    {
        Program.DrawPopup += RenderGame;
        Program.DrawPopup += DebugPPU;
        Program.DrawPopup += DebugVRAM;

        CreateSpriteSheets();
    }

    public void ResetRomData()
    {
        LoadSpritesRawData();

        UpdateSpriteSheet();
        UpdateNametablesSheet();
    }

    public void Tick()
    {
        ushort addr = 0;//Convert.ToUInt16(motherBoard.Read(0));

        if ((addr >= 0x2000 && addr <= 0x3FFF) || addr == 0x4014)
        {
            byte regIndex = (byte)(addr != 0x4014 ?((addr - 0x2000) % 8) : 14);

            Console.WriteLine($"reading ppu register {regIndex}");

        }

        Draw();
    }

    public void Draw()
    {
        // rendering background

        byte _finex = 0; //(byte)(_scrollX & 0b_0000_0111);
        byte _finey = 0; //(byte)(_scrollY & 0b_0000_0111);

        for (int tx = -1; tx < 32; tx++)
        {
            for (int ty = -1; ty < 30; ty++)
            {

                int _stx = tx + (_scrollX >> 3);
                int _sty = ty + (_scrollY >> 3);

                int tileAddr = _stx + _sty * 32;
                int palleteAddr = tx/4 + ty/4 * 8;

                //if (tileAddr >= 0x400 - 0x40) tileAddr += 0x40;
                //if (tileAddr >= 0x800 - 0x40) tileAddr += 0x40;
                //if (tileAddr >= 0xC00 - 0x40) tileAddr += 0x40;

                byte tileindex = ReadPPUData((ushort)(0x2000 + tileAddr));
                byte paleteInfo = ReadPPUData((ushort)(0x23C0 + palleteAddr));

                int tIndex = (((tx/2) % 2) + (((ty/2) % 2) * 2));
                int attributeIndex = ((paleteInfo >> (tIndex * 2)) & 0b_11) * 4;

                int palleteIndex0 = ReadPPUData(0x3F00);
                int palleteIndex1 = ReadPPUData((ushort)(0x3F01 + attributeIndex));
                int palleteIndex2 = ReadPPUData((ushort)(0x3F02 + attributeIndex));
                int palleteIndex3 = ReadPPUData((ushort)(0x3F03 + attributeIndex));

                (byte r, byte g, byte b)[] colors = [
                    palletes[palleteIndex0], palletes[palleteIndex2],
                    palletes[palleteIndex1], palletes[palleteIndex3],
                ];

                for (int px = 0; px < 8; px++) for (int py = 0; py < 8; py++)
                    {
                        int pixelValue = _vram_chr[tileindex + (backgroundPatternTable == 1 ? 256 : 0), px, py];

                        var pixelIndex = ((tx * 8 + px - _finex) + (ty * 8 + py - _finey) * 32 * 8) * 3;
                        if (pixelIndex < 0 || pixelIndex >= _video_out_buffer.Length) continue;

                        _video_out_buffer[pixelIndex + 0] = colors[pixelValue].r;
                        _video_out_buffer[pixelIndex + 1] = colors[pixelValue].g;
                        _video_out_buffer[pixelIndex + 2] = colors[pixelValue].b;
                    }
            }
        }

        // rendering foreground
        byte[][] secondaryOam = new byte[8][];
        for (int py = 0; py < 240; py++)
        {

            byte[][] tempSecondaryOam = new byte[8][];
            byte spriteIndex = 0;

            for (int sprite = 0; sprite < 64; sprite++)
            {
                var spriteY = _vram_oam[sprite * 4];
                if (spriteY > 0 && spriteY == py) tempSecondaryOam[spriteIndex++] = _vram_oam[(sprite * 4)..((sprite + 1) * 4)];
                if (spriteIndex == 8) break;
            }

            // draw sprites on secondaryOam Queue
            foreach (var sprite in secondaryOam)
            {
                if (sprite == null) continue;

                byte spriteX = sprite[3];

                byte tileIndex = sprite[1];
                byte attributes = sprite[2];

                bool flipX = (attributes & 0b_0100_0000) != 0;
                bool flipY = (attributes & 0b_1000_0000) != 0;

                byte pallete = (byte)(attributes & 0b11);

                int palleteIndex0 = ReadPPUData(0x3F10);
                int palleteIndex1 = ReadPPUData((ushort)(0x3F11 + pallete));
                int palleteIndex2 = ReadPPUData((ushort)(0x3F12 + pallete));
                int palleteIndex3 = ReadPPUData((ushort)(0x3F13 + pallete));

                (byte r, byte g, byte b)[] colors = [
                    palletes[palleteIndex0], palletes[palleteIndex2],
                    palletes[palleteIndex1], palletes[palleteIndex3],
                ];

                for (int tpx = 0; tpx < 8; tpx++)
                {
                    if (spriteX + tpx >= 255) continue;

                    for (int tpy = 0; tpy < 8; tpy++)
                    {
                        if (py + tpy >= 240) continue;

                        int pixelValue = _vram_chr[tileIndex + (spritePatternTable == 0 ? 0 : 256), flipX ? (7 - tpx) : (tpx), flipY ? (7 - tpy) : (tpy)];
                        if (pixelValue == 0) continue;

                        var pixelIndex = (spriteX + tpx + (py + tpy) * 32 * 8) * 3;

                        _video_out_buffer[pixelIndex + 0] = colors[pixelValue].r;
                        _video_out_buffer[pixelIndex + 1] = colors[pixelValue].g;
                        _video_out_buffer[pixelIndex + 2] = colors[pixelValue].b;

                    }
                }
            }

            secondaryOam = tempSecondaryOam;
        }

        UpdateVideo();
        if (vBlankNMInterrupt) system.Cpu.RequestNMInterrupt();
    }

    public void UpdateVideo()
    {
        var gl = Program.gl;
        gl.BindTexture(TextureTarget.Texture2D, _video_out_texture);
        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 256, 240, 0,
            PixelFormat.Rgb, PixelType.UnsignedByte, _video_out_buffer);

        if (_updateNametablesSheet)
        {
            UpdateNametablesSheet();
            _updateNametablesSheet = false;
        }
    }

    public byte ReadRegister(ushort addr)
    {
        int regIndex = (addr - 0x2000) % 8;
        switch (regIndex) {
        
            case 2: _wLatch = false; return _ppustat;
            case 4: return _oamdata;
            case 7: return ReadPPUData(_currVRAMAddr);

            default:
                Console.WriteLine($"PPU Register {regIndex} is not readable!");
                break;

        };

        return 0;
    }
    public void WriteRegister(ushort addr, byte data)
    {
        if (addr == 0x4014)
        {
            CopyOAMData(data);
            return;
        }

        int regIndex = (addr - 0x2000) % 8;
        switch (regIndex)
        {
            case 0: WritePPUCtrl(data); break;
            case 1: _ppumask = data; break;
            case 3: _oamaddr = data; break;
            case 4: _oamdata = data; break;
            case 5: WritePPUScroll(data); break;
            case 6: WritePPUAddress(data); break;
            case 7: WritePPUData(data); break;

            default:
                Console.WriteLine($"PPU Register {regIndex} is not writeable!");
                break;

        };
    }

    // debug shit
    private uint _spriteSheetHandlerLeft = 0;
    private uint _spriteSheetHandlerRight = 0;
    private byte _viewingSheet = 0;
    private int _palleteIndexA = 1;
    private int _palleteIndexB = 17;
    private int _palleteIndexC = 2;
    private uint _nametablesSheetHandler = 0;
    private bool _showNametablesAttributeTable = false;
    private bool _updateNametablesSheet = false;

    private void DebugPPU()
    {
        ImGui.Begin("PPU Debug");
        {
            ImGui.SeparatorText("Control:");;

            if (vBlankNMInterrupt) ImGui.Text("NMI enabled"); else ImGui.TextDisabled("NMI Disabled");
            if (isMaster) ImGui.Text("PPU Master"); else ImGui.TextDisabled("PPU Slave");

            ImGui.TextDisabled("Sprite size:"); ImGui.SameLine();
            ImGui.Text(spriteHeight == 16 ? "8x16" : "8x8");

            ImGui.TextDisabled("FG sheet:"); ImGui.SameLine();
            ImGui.Text(spritePatternTable == 1 ? "Right" : "Left");
            ImGui.TextDisabled("BG sheet:"); ImGui.SameLine();
            ImGui.Text(backgroundPatternTable == 1 ? "Right" : "Left");


            ImGui.TextDisabled("VRAM inc:"); ImGui.SameLine();
            ImGui.Text($"x{incrementPerRead}");

            ImGui.SeparatorText("Flags:");
            ImGui.Text($"{_ppustat:b8}");

            ImGui.SeparatorText("Data:");
            ImGui.TextDisabled("Temp Addr:"); ImGui.SameLine();
            ImGui.Text($"${_tempVRAMAddr:X4} ({_tempVRAMAddr:b16})");
            ImGui.TextDisabled("Data Addr:"); ImGui.SameLine();
            ImGui.Text($"${_currVRAMAddr:X4} ({_currVRAMAddr:b16})");

            ImGui.SeparatorText("Scroll:");
            ImGui.TextDisabled("X scroll:"); ImGui.SameLine();
            ImGui.Text($"{_scrollX}");
            ImGui.TextDisabled("Y scroll:"); ImGui.SameLine();
            ImGui.Text($"{_scrollY}");
        }
        ImGui.End();

        ImGui.Begin("Sprite Sheet View", ImGuiWindowFlags.AlwaysAutoResize);
        {
            ImGui.Image((nint)(_viewingSheet == 0 ? _spriteSheetHandlerLeft : _spriteSheetHandlerRight), new(700, 700));

            if (ImGui.Button("Left Sheet")) _viewingSheet = 0;
            ImGui.SameLine();
            if (ImGui.Button("Right Sheet")) _viewingSheet = 1;

            ImGui.Text("Pallete:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("A", ref _palleteIndexA, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (_palleteIndexA > 64) _palleteIndexA -= 64;
                if (_palleteIndexA < 1) _palleteIndexA += 64;

                UpdateSpriteSheet();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("B", ref _palleteIndexB, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (_palleteIndexB > 64) _palleteIndexB -= 64;
                if (_palleteIndexB < 1) _palleteIndexB += 64;

                UpdateSpriteSheet();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("C", ref _palleteIndexC, 1, 1, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (_palleteIndexC > 64) _palleteIndexC -= 64;
                if (_palleteIndexC < 1) _palleteIndexC += 64;

                UpdateSpriteSheet();
            }

        }
        ImGui.End();
    }
    private void DebugVRAM()
    {
        ImGui.Begin("VRAM View", ImGuiWindowFlags.AlwaysAutoResize);
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList(); ;

            ImGui.SeparatorText("Pattern Tables:");

            ImGui.Image((nint)_spriteSheetHandlerLeft, new(300, 300)); ImGui.SameLine();
            ImGui.Image((nint)_spriteSheetHandlerRight, new(300, 300));

            ImGui.SeparatorText("Nametables:");

            float maxW = ImGui.GetContentRegionAvail().X;
            Vector2 renderRes = new(256, 240);

            float imageAspectRatio = renderRes.X / renderRes.Y;
            Vector2 finalSize = new(maxW, maxW / imageAspectRatio);
            float scale = finalSize.X / renderRes.X / 2;

            var cp = ImGui.GetCursorScreenPos();

            ImGui.Image((nint)_nametablesSheetHandler, finalSize);

            drawList.AddRect(
                cp + (new Vector2(_scrollX * scale, _scrollY * scale)),
                cp + (new Vector2((_scrollX + 256) * scale, (_scrollY + 240) * scale)),
                ImGui.GetColorU32(new Vector4(1f, 0f, 0f, 1f))
            );

            if (ImGui.Checkbox("Show attributes", ref _showNametablesAttributeTable)) _updateNametablesSheet = true;

            ImGui.NewLine();

            for (var i = 0; i < 30; i++)
            {
                ImGui.TextDisabled($"{0x2000 + i * 32:X4}:"); ImGui.SameLine();
                ImGui.Text(string.Join(" ", _vram_nametable[(i * 32) .. ((i+1) * 32)].Select(e => $"{e:X2}")));
            }
            ImGui.NewLine();
            for (var i = 30; i < 32; i++)
            {
                ImGui.TextDisabled($"{0x2000 + i * 32:X4}:"); ImGui.SameLine();
                ImGui.Text(string.Join(" ", _vram_nametable[(i * 32)..((i + 1) * 32)].Select(e => $"{e:X2}")));
            }
            ImGui.NewLine();
            for (var i = 32; i < 62; i++)
            {
                ImGui.TextDisabled($"{0x2000 + i * 32:X4}:"); ImGui.SameLine();
                ImGui.Text(string.Join(" ", _vram_nametable[(i * 32)..((i + 1) * 32)].Select(e => $"{e:X2}")));
            }
            ImGui.NewLine();
            for (var i = 62; i < 64; i++)
            {
                ImGui.TextDisabled($"{0x2000 + i * 32:X4}:"); ImGui.SameLine();
                ImGui.Text(string.Join(" ", _vram_nametable[(i * 32)..((i + 1) * 32)].Select(e => $"{e:X2}")));
            }

            ImGui.SeparatorText("Palletes:");

            ImGui.TextDisabled("Background:");

            var cursorPos = ImGui.GetCursorScreenPos();
            var cpx = cursorPos.X;
            var cpy = cursorPos.Y;

            for (var i = 0; i < 16; i++)
            {
                var c = palletes[ReadPPUData((ushort)(0x3F00 + i))];
                var color = ImGui.GetColorU32(new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f));

                var posA = new Vector2(cpx + (33 * i), cpy);
                var posB = new Vector2(cpx + (33 * (i + 1) - 1), cpy + 32);
                drawList.AddRectFilled(posA, posB, color);
            }
            ImGui.Dummy(new(32 * 16, 32));

            ImGui.TextDisabled($"{0x3F00:X4}:"); ImGui.SameLine();
            ImGui.Text(string.Join(" ", _vram_atbttable[0x00..0x10].Select(e => $"{e:X2}")));

            ImGui.TextDisabled("Sprites:");

            cursorPos = ImGui.GetCursorScreenPos();
            cpx = cursorPos.X;
            cpy = cursorPos.Y;

            for (var i = 0; i < 16; i++)
            {
                var c = palletes[ReadPPUData((ushort)(0x3F10 + i))];
                var color = ImGui.GetColorU32(new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f));

                var posA = new Vector2(cpx + (33 * i), cpy);
                var posB = new Vector2(cpx + (33 * (i+1) - 1), cpy + 32);

                drawList.AddRectFilled(posA, posB, color, 0);
            }
            ImGui.Dummy(new(32 * 16, 32));

            ImGui.TextDisabled($"{0x3F10:X4}:"); ImGui.SameLine();
            ImGui.Text(string.Join(" ", _vram_atbttable[0x10..0x20].Select(e => $"{e:X2}")));
        }
        ImGui.End();
    }
    private void RenderGame()
    {
        ImGui.Begin("Video Out");

        Vector2 viewSize = ImGui.GetContentRegionAvail();
        Vector2 renderRes = new(256, 240);

        float canvasAspectRatio = viewSize.X / viewSize.Y;
        float imageAspectRatio = renderRes.X / renderRes.Y;

        Vector2 finalSize = (canvasAspectRatio < imageAspectRatio)
            ? new Vector2(viewSize.X, viewSize.X / imageAspectRatio)
            : new Vector2(viewSize.Y * imageAspectRatio, viewSize.Y);

        ImGui.Image((nint)_video_out_texture, finalSize);

        ImGui.End();
    }

    private void CreateSpriteSheets()
    {
        var gl = Program.gl;
        _spriteSheetHandlerLeft = gl.GenTexture();
        _spriteSheetHandlerRight = gl.GenTexture();
        _nametablesSheetHandler = gl.GenTexture();
        _video_out_texture = gl.GenTexture();

        gl.BindTexture(TextureTarget.Texture2D, _spriteSheetHandlerLeft);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, in _texMinFilter);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, in _texMagFilter);

        gl.BindTexture(TextureTarget.Texture2D, _spriteSheetHandlerRight);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, in _texMinFilter);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, in _texMagFilter);

        gl.BindTexture(TextureTarget.Texture2D, _nametablesSheetHandler);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, in _texMinFilter);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, in _texMagFilter);
        
        gl.BindTexture(TextureTarget.Texture2D, _video_out_texture);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, in _texWrapMode);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, in _texMinFilter);
        gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, in _texMagFilter);
    }
    private void UpdateSpriteSheet()
    {
        var imageData = GetSpritesRGBData();
        var imageDataLeft = imageData.AsSpan(0, imageData.Length / 2);
        var imageDataRIght = imageData.AsSpan(imageData.Length / 2, imageData.Length / 2);

        var gl = Program.gl;
        gl.BindTexture(TextureTarget.Texture2D, _spriteSheetHandlerLeft);
        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 128, 128, 0,
            PixelFormat.Rgb, PixelType.UnsignedByte, imageDataLeft);

        gl.BindTexture(TextureTarget.Texture2D, _spriteSheetHandlerRight);
        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 128, 128, 0,
            PixelFormat.Rgb, PixelType.UnsignedByte, imageDataRIght);
    }

    private void UpdateNametablesSheet()
    {
        byte[] buf = new byte[64 * 60 * 8 * 8 * 3];

        for (int nametable = 0; nametable < 4; nametable++)
        {
            for (int tx = 0; tx < 32; tx++)
            {
                for (int ty = 0; ty < 30; ty++)
                {

                    int tileAddr = tx + ty * 32;
                    int palleteAddr = 0x3C0 + (tx/4) + ((ty/4) * 8);

                    tileAddr += 0x400 * nametable;
                    palleteAddr += 0x400 * nametable;

                    byte tileindex = ReadPPUData((ushort)(0x2000 + tileAddr));
                    byte paleteInfo = ReadPPUData((ushort)(0x2000 + palleteAddr));

                    int tIndex = (((tx / 2) % 2) + (((ty / 2) % 2) * 2));
                    int attributeIndex = ((paleteInfo >> (tIndex * 2)) & 0b_11) * 4;

                    int palleteIndex0 = ReadPPUData((ushort)(0x3F00));
                    int palleteIndex1 = ReadPPUData((ushort)(0x3F01 + attributeIndex));
                    int palleteIndex2 = ReadPPUData((ushort)(0x3F02 + attributeIndex));
                    int palleteIndex3 = ReadPPUData((ushort)(0x3F03 + attributeIndex));

                    (byte r, byte g, byte b)[] colors = [
                        palletes[palleteIndex0],
                        palletes[palleteIndex2],
                        palletes[palleteIndex1],
                        palletes[palleteIndex3],
                    ];

                    for (int px = 0; px < 8; px++)
                    {
                        for (int py = 0; py < 8; py++)
                        {
                            int pixelValue;

                            if (!_showNametablesAttributeTable)
                                pixelValue = _vram_chr[tileindex + (backgroundPatternTable == 1 ? 256 : 0), px, py];
                            else
                                pixelValue = ((px/4) % 2) + ((py/4) % 2) * 2;

                            var tbx = nametable % 2 != 0 ? 32 : 0;
                            var tby = nametable >= 2 ? 30 : 0;

                            var pixelIndex = (((tbx + tx) * 8 + px) + ((tby + ty) * 8 + py) * 64 * 8) * 3;

                            buf[pixelIndex + 0] = colors[pixelValue].r;
                            buf[pixelIndex + 1] = colors[pixelValue].g;
                            buf[pixelIndex + 2] = colors[pixelValue].b;
                        }
                    }
                }
            }
        }

        var gl = Program.gl;
        gl.BindTexture(TextureTarget.Texture2D, _nametablesSheetHandler);
        gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgb, 64 * 8, 60 * 8, 0,
            PixelFormat.Rgb, PixelType.UnsignedByte, buf);
    }

    private void LoadSpritesRawData()
    {
        for (var tx = 0; tx < 16; tx++)
        {
            for (var ty = 0; ty < 32; ty++)
            {
                int tileIndex = tx + ty * 16;

                for (var sl = 0; sl < 8; sl++)
                {
                    byte sl1 = Read(tileIndex * 16 + sl);
                    byte sl2 = Read(tileIndex * 16 + 8 + sl);

                    for (var sc = 7; sc >= 0; sc--)
                    {
                        int pixelValue = (((sl1 >> sc) & 1) << 1) | (((sl2 >> sc) & 1));

                        _vram_chr[tx + ty * 16, 7 - sc, sl] = (byte)pixelValue;
                    }
                }

            }
        }
    }
    private byte[] GetSpritesRGBData()
    {
        var imageData = new byte[16 * 32 * 8 * 8 * 3];

        for (var tx = 0; tx < 16; tx++)
        {
            for (var ty = 0; ty < 32; ty++)
            {
                for (var px = 0; px < 8; px++)
                {
                    for (var py = 0; py < 8; py++)
                    {

                        var pixelValue = _vram_chr[tx + ty * 16, px, py];

                        (int r, int g, int b) = pixelValue switch
                        {
                            1 => palletes[_palleteIndexA - 1],
                            2 => palletes[_palleteIndexB - 1],
                            3 => palletes[_palleteIndexC - 1],

                            _ => (0, 0, 0)
                        };

                        var pixelIndex = ((tx * 8) + px + (ty * 16 * 8 * 8) + (py * 16 * 8)) * 3;

                        imageData[pixelIndex + 0] = (byte)r;
                        imageData[pixelIndex + 1] = (byte)g;
                        imageData[pixelIndex + 2] = (byte)b;

                    }
                }

            }
        }

        return imageData;
    }

    private byte Read(int addr) => system.Read((ushort)addr, ReadingAs.PPU);

}
