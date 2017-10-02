﻿using System.IO;
using Vostok.Airlock;
using Vostok.Commons.Binary;

namespace Vostok.AirlockConsumer
{
    public class SimpleAirlockSink : IAirlockSink
    {
        public SimpleAirlockSink(Stream stream)
        {
            WriteStream = stream;
            Writer = new SimpleBinaryWriter(stream);
        }

        public Stream WriteStream { get; }
        public IBinaryWriter Writer { get; }
    }
}