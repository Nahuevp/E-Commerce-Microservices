-- Inventory Service Database Schema
-- Run this script to initialize the database tables

-- Create Inventory table
CREATE TABLE IF NOT EXISTS "Inventories" (
    "Id" SERIAL PRIMARY KEY,
    "ProductId" INT NOT NULL UNIQUE,
    "AvailableStock" INT NOT NULL DEFAULT 0 CHECK (AvailableStock >= 0),
    "ReservedStock" INT NOT NULL DEFAULT 0 CHECK (ReservedStock >= 0),
    "TotalStock" INT NOT NULL
);

-- Create Reservations table
CREATE TABLE IF NOT EXISTS "Reservations" (
    "Id" SERIAL PRIMARY KEY,
    "ProductId" INT NOT NULL,
    "Quantity" INT NOT NULL CHECK (Quantity > 0),
    "ReservationCode" VARCHAR(50) UNIQUE NOT NULL,
    "Status" VARCHAR(20) DEFAULT 'Active',
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP,
    CONSTRAINT "FK_Reservations_Inventories_ProductId" 
        FOREIGN KEY ("ProductId") 
        REFERENCES "Inventories"("ProductId") 
        ON DELETE RESTRICT
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "IX_Reservations_Status" ON "Reservations"("Status");
CREATE INDEX IF NOT EXISTS "IX_Reservations_ExpiresAt" ON "Reservations"("ExpiresAt");
CREATE INDEX IF NOT EXISTS "IX_Reservations_ProductId" ON "Reservations"("ProductId");

-- Insert sample data (optional)
-- INSERT INTO "Inventories" ("ProductId", "AvailableStock", "ReservedStock", "TotalStock")
-- VALUES 
--     (1, 100, 0, 100),
--     (2, 50, 0, 50),
--     (3, 200, 0, 200)
-- ON CONFLICT ("ProductId") DO NOTHING;

COMMENT ON TABLE "Inventories" IS 'Tracks available, reserved, and total stock for products';
COMMENT ON TABLE "Reservations" IS 'Tracks stock reservations with 15-minute TTL';
COMMENT ON COLUMN "Inventories"."AvailableStock" IS 'Stock available for new reservations';
COMMENT ON COLUMN "Inventories"."ReservedStock" IS 'Stock currently reserved but not yet sold';
COMMENT ON COLUMN "Inventories"."TotalStock" IS 'Total stock (AvailableStock + ReservedStock)';
COMMENT ON COLUMN "Reservations"."ReservationCode" IS 'Unique code like RES-ABC123 for human reference';
COMMENT ON COLUMN "Reservations"."Status" IS 'Active, Confirmed, Cancelled, or Expired';
COMMENT ON COLUMN "Reservations"."ExpiresAt" IS 'CreatedAt + 15 minutes for automatic expiration';
