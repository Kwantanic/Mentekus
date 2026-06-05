FROM ollama/ollama:latest

# Needed for the startup warm-up request
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy an initialization script (best practice)
COPY init-models.sh /init-models.sh
RUN chmod +x /init-models.sh

# Use the script as entrypoint
ENTRYPOINT ["/init-models.sh"]