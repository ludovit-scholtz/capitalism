-- Idempotent bootstrap for clean Docker volumes.
-- The postgres entrypoint runs this once when /var/lib/postgresql/data is empty.
CREATE DATABASE gamemaster;
CREATE DATABASE game1;
