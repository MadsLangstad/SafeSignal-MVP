#!/bin/bash
# Generate realistic TTS audio for emergency alerts using macOS 'say' command

OUTPUT_DIR="/tmp/safesignal_audio"
mkdir -p "$OUTPUT_DIR"

echo "Generating TTS audio clips for SafeSignal..."

# FIRE_ALARM - High severity
say -v Samantha -r 180 -o "$OUTPUT_DIR/FIRE_ALARM.aiff" \
    "Attention. Fire alarm activated. This is not a drill. Evacuate the building immediately using the nearest exit. Do not use elevators. Proceed to your designated assembly point."

# EVACUATION - High severity
say -v Samantha -r 180 -o "$OUTPUT_DIR/EVACUATION.aiff" \
    "Attention. Emergency evacuation in progress. Leave the building immediately. Follow emergency exit signs. Do not return to the building until cleared by authorities."

# LOCKDOWN - Critical severity
say -v Alex -r 200 -o "$OUTPUT_DIR/LOCKDOWN.aiff" \
    "Attention. Lockdown initiated. Proceed to the nearest secure room immediately. Lock all doors. Turn off lights. Remain silent. Do not open doors until cleared by authorities."

# SEVERE_WEATHER - Medium severity
say -v Samantha -r 160 -o "$OUTPUT_DIR/SEVERE_WEATHER.aiff" \
    "Attention. Severe weather alert. Seek shelter immediately in interior rooms away from windows. Remain indoors until the all-clear is given."

# MEDICAL_EMERGENCY - Medium severity
say -v Samantha -r 160 -o "$OUTPUT_DIR/MEDICAL_EMERGENCY.aiff" \
    "Attention. Medical emergency in progress. Clear the area. Emergency personnel are responding. If you have medical training and can assist, please identify yourself to security."

# ALL_CLEAR - Low severity
say -v Samantha -r 140 -o "$OUTPUT_DIR/ALL_CLEAR.aiff" \
    "Attention. The emergency has ended. It is now safe to resume normal activities. Thank you for your cooperation."

# SHELTER_IN_PLACE - Medium severity
say -v Samantha -r 160 -o "$OUTPUT_DIR/SHELTER_IN_PLACE.aiff" \
    "Attention. Shelter in place. Move to interior rooms. Close all windows and doors. Turn off ventilation systems. Await further instructions."

# CHEMICAL_HAZARD - High severity
say -v Alex -r 180 -o "$OUTPUT_DIR/CHEMICAL_HAZARD.aiff" \
    "Attention. Chemical hazard detected. Evacuate the area immediately. Move upwind and uphill if outdoors. Seek emergency decontamination if exposed."

echo ""
echo "Converting AIFF to WAV format..."

# Convert all AIFF files to WAV
for file in "$OUTPUT_DIR"/*.aiff; do
    basename=$(basename "$file" .aiff)
    afconvert -f WAVE -d LEI16@44100 "$file" "$OUTPUT_DIR/${basename}.wav"
    rm "$file"  # Remove AIFF file after conversion
done

echo ""
echo "✅ Generated audio files:"
ls -lh "$OUTPUT_DIR"/*.wav

echo ""
echo "📁 Audio files saved to: $OUTPUT_DIR"
echo ""
echo "To upload to MinIO, run:"
echo "  docker exec -it safesignal-minio mc cp $OUTPUT_DIR/*.wav /data/audio-clips/"
