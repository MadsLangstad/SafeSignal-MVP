#
# Component Makefile
#

COMPONENT_ADD_INCLUDEDIRS := . ../include ../certs

# Embed certificates as binary data
COMPONENT_EMBED_TXTFILES := ../certs/ca.crt ../certs/client.crt ../certs/client.key

COMPONENT_SRCDIRS := .
