﻿using System;
using System.IO;
using Vostok.Airlock;
using Vostok.Commons.Binary;

namespace Vostok.AirlockConsumer.UnitTests
{
    public class NonReusableByteBufferAirlockSink : IAirlockSink
    {
        private readonly BinaryBufferWriter bufferWriter = new BinaryBufferWriter(1024);

        public IBinaryWriter Writer => bufferWriter;

        public Stream WriteStream => throw new NotImplementedException();

        public byte[] FilledBuffer => bufferWriter.FilledSegment.ToArray();
    }
}