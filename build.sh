#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Load environment variables
if [ -f ".env" ]; then
    while IFS='=' read -r key value || [[ -n "$key" ]]; do
        # Skip comments and empty lines
        [[ "$key" =~ ^#.*$ ]] && continue
        [[ -z "$key" ]] && continue
        # Remove carriage return if present (Windows line endings)
        value="${value%$'\r'}"
        # Convert backslashes to forward slashes
        value="${value//\\//}"
        # Export the variable
        export "$key=$value"
    done < .env
else
    echo "Error: .env file not found. Copy .env.example to .env and configure it."
    exit 1
fi

# Defaults
BUILD_CONFIG=${BUILD_CONFIG:-Debug}
MOD_NAME="ItemConduit"
PROJECT_DIR="$SCRIPT_DIR/ItemConduit"

echo "=== ItemConduit Build Script ==="
echo "Build: $BUILD_CONFIG"
echo ""

# Step 1: Clean rebuild
echo "[1/4] Cleaning previous build..."
dotnet clean "$PROJECT_DIR" -c "$BUILD_CONFIG" -v q 2>/dev/null || true
rm -rf "$PROJECT_DIR/bin/$BUILD_CONFIG" 2>/dev/null || true

echo "[2/4] Building solution..."
dotnet build "$PROJECT_DIR" -c "$BUILD_CONFIG"

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

OUTPUT_DIR="$PROJECT_DIR/bin/$BUILD_CONFIG/net462"
DLL_FILE="$OUTPUT_DIR/$MOD_NAME.dll"
PDB_FILE="$OUTPUT_DIR/$MOD_NAME.pdb"
MDB_FILE="$OUTPUT_DIR/$MOD_NAME.dll.mdb"

# Step 3: Generate .mdb for Debug builds
if [ "$BUILD_CONFIG" == "Debug" ]; then
    echo "[3/4] Generating .mdb file for debugging..."
    if [ -f "$SCRIPT_DIR/pdb2mdb.exe" ]; then
        if command -v mono &> /dev/null; then
            mono "$SCRIPT_DIR/pdb2mdb.exe" "$DLL_FILE"
        else
            "$SCRIPT_DIR/pdb2mdb.exe" "$DLL_FILE"
        fi
        echo "  Created: $MDB_FILE"
    else
        echo "  Warning: pdb2mdb.exe not found in solution directory, skipping .mdb generation"
    fi
else
    echo "[3/4] Release build - skipping .mdb generation"
fi

# Step 4: Copy to game directories
echo "[4/4] Copying files to game directories..."

copy_files() {
    local dest="$1"
    local name="$2"

    if [ -z "$dest" ]; then
        echo "  $name: Not configured, skipping"
        return
    fi

    if [ ! -d "$dest" ]; then
        echo "  $name: Directory not found ($dest), creating..."
        mkdir -p "$dest"
    fi

    if [ -f "$DLL_FILE" ]; then
        cp "$DLL_FILE" "$dest/"
        echo "  $name: Copied $MOD_NAME.dll"
    fi

    if [ -f "$PDB_FILE" ]; then
        cp "$PDB_FILE" "$dest/"
        echo "  $name: Copied $MOD_NAME.pdb"
    fi

    if [ -f "$MDB_FILE" ]; then
        cp "$MDB_FILE" "$dest/"
        echo "  $name: Copied $MOD_NAME.dll.mdb"
    fi
}

copy_files "$CLIENT_PATH" "Client"
copy_files "$SERVER_PATH" "Server"

echo ""
echo "=== Build Complete ==="
