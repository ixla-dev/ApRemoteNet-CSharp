using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Wrapper;

internal class Program
{
    public struct InkUsageResponse
    {
        public string ColorName { get; set; }
        public ulong InkUsagePicoLiters { get; set; }
    }

    private static void Main(string[] args)
    {
        var apRemote = new apRemoteNet.APRemote_Net();
        
        var    client         = new APrintWrapper("192.168.3.141", 1025);
        double dropVolumeSize = 0;
        if (args.Length == 0)
        {
            while (true)
            {
                Console.Write("drop volume size: ");
                var input = Console.ReadLine();
                dropVolumeSize = Convert.ToDouble(input);
                client.PrintInkUsage(dropVolumeSize);
                Console.WriteLine("-------------------");
            }
        }
        else
            dropVolumeSize = Convert.ToDouble(args[0]);

        client.PrintInkUsage(dropVolumeSize);
    }

    public class APrintWrapper
    {
        private readonly TcpClient     _client;
        private readonly NetworkStream _ns;

        public APrintWrapper(string ip, int port)
        {
            _client = new TcpClient(ip, port);
            _ns = _client.GetStream();
        }

        public void PrintInkUsage(double dropVolumeSize)
        {
            var colors = CalculateInkUsage(dropVolumeSize);
            for (ushort c = 0; c < colors; c++)
            {
                var usage = GetInkUsageResult(c);
                Console.WriteLine($"c = {c}, usage = {usage}");
            }
        }

        public long GetInkUsageResult(ushort color)
        {
            var buffer = new Memory<byte>(new byte[10]);
            var cmd    = "ig"u8.ToArray().AsMemory();
            cmd.CopyTo(buffer[..2]);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4, 2).Span, color);
            "\n\0"u8.ToArray().AsMemory().CopyTo(buffer.Slice(8, 2));
            _ns.Write(buffer.Span);

            var response = new Memory<byte>(new byte[256]);
            var lenght   = _ns.Read(response.Span);

            var ink_usage  = BinaryPrimitives.ReadInt64LittleEndian(response.Slice(6, 8).Span);
            var count      = BinaryPrimitives.ReadUInt16LittleEndian(response.Span.Slice(14, 2));
            var color_name = Encoding.Unicode.GetString(response.Slice(16, lenght - (16 + 2 + 2)).Span);
            Console.WriteLine($"{color_name}, ink_usage = {ink_usage}, count = {count}");
            return ink_usage;
        }

        public ushort CalculateInkUsage(double dropVolume)
        {
            var buffer = new Memory<byte>(new byte[10]);
            var cmd    = "ic"u8.ToArray().AsMemory();
            cmd.CopyTo(buffer[..2]);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4).Span, (uint)Math.Round(dropVolume * 100));
            "\n\0"u8.ToArray().AsMemory().CopyTo(buffer.Slice(8, 2));
            _ns.Write(buffer.Span);

            // rp (2) + uint (4) + uint (4) + "ic" (s) 
            var response = new Memory<byte>(new byte[12]);
            var read     = _ns.Read(response.Span);

            var rp         = Encoding.ASCII.GetString(response.Slice(0, 2).Span);
            var error_code = BinaryPrimitives.ReadUInt32LittleEndian(response.Slice(2, 4).Span);
            var colors     = BinaryPrimitives.ReadUInt16LittleEndian(response.Slice(6, 2).Span);
            var command    = Encoding.ASCII.GetString(response.Slice(8, 2).Span);

            if (error_code != 0)
            {
                var message = GetErrorString(error_code);
                throw new Exception(message);
            }

            return colors;
        }

        public string GetErrorString(uint error_code)
        {
            var args = new Memory<byte>(new byte[4]);
            BinaryPrimitives.WriteUInt32BigEndian(args[..4].Span, error_code);
            SendCommand("ge", args);
            var message = ReadUnicodeString();
            return message;
        }

        public string ReadUnicodeString()
        {
            var buffer = new Memory<byte>(new byte[1024]);
            _ns.Read(buffer.Span);
            return Encoding.Unicode.GetString(buffer.Span);
        }

        public void SendCommand(string command, Memory<byte> args)
        {
            var buffer        = new Memory<byte>(new byte[args.Length+ 4]);
            var command_bytes = Encoding.ASCII.GetBytes(command).AsMemory();
            command_bytes.CopyTo(buffer[..2]);
            args.CopyTo(buffer.Slice(2, args.Length));
            "\n\0"u8.ToArray().AsMemory().CopyTo(buffer[(args.Length + 4)..]);
            _ns.Write(buffer.Span);
        }

        public static int SizeOf<T>() where T : struct => Marshal.SizeOf<T>();
    }
}