-- Initialize WorkerService database
-- This script is run when the PostgreSQL container starts

-- Create additional schemas if needed
-- CREATE SCHEMA IF NOT EXISTS app;

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE workerservice TO worker;

-- Create extensions if needed
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Insert sample data (optional)
-- This will be created by EF Core migrations