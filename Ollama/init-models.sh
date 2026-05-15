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

# Keep container running
wait $pid