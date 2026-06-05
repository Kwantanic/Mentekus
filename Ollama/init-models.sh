#!/bin/bash

# Start Ollama server in background
ollama serve &
pid=$!

# Wait for server to be ready
sleep 5

echo "🔄 Pulling models for Mentekus..."
ollama pull qwen3-embedding:0.6b
ollama pull qwen3:4b-instruct

echo "✅ Models ready. Starting Mentekus agent layer..."

echo "🔥 Warming embedding model..."
curl -fsS http://localhost:11434/api/embed \
    -H "Content-Type: application/json" \
    -d '{
        "model": "qwen3-embedding:0.6b",
        "input": "warm up embedding model",
        "keep_alive": "30m"
    }' > /dev/null

# Keep container running
wait $pid