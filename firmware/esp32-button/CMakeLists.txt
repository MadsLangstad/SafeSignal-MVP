# SafeSignal ESP32-S3 Button Firmware
# CMake build system for ESP-IDF 5.x

cmake_minimum_required(VERSION 3.16)

# Project name
set(PROJECT_NAME "safesignal-button")

# Include ESP-IDF CMake
include($ENV{IDF_PATH}/tools/cmake/project.cmake)

# Define project
project(${PROJECT_NAME})
