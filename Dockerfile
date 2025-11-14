n# Container image for deploying the indicator leasing app to Cloud Run
FROM python:3.12-slim

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

# Install dependencies first for better layer caching
COPY indicator_leasing/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code (including the indicator bundle)
COPY indicator_leasing/ ./

# Cloud Run expects the service to listen on $PORT
ENV PORT=8080

# Use gunicorn in production
CMD ["gunicorn", "-b", "0.0.0.0:8080", "app:app"]
