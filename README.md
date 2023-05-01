### Discord ChatBot

Talks, takes selfies, and posts photos using locally-ran large language models running on your GPU.

Inspired by DeSinc's project here: https://github.com/DeSinc/SallyBot.

### Quickstart

```shell
# Set up oobabooga and stable-diffusion-webui in ..
cd ..
# See https://github.com/DeSinc/SallyBot
# https://github.com/oobabooga/text-generation-webui
# https://github.com/AUTOMATIC1111/stable-diffusion-webui
setup oobabooga
setup stable-diffusion-webui
cd ChatBot
# Copy the sample config and edit it
cp src/Application/config.sample.json src/Application/config.json
edit src/Application/config.json
# Deploy on Linux
./build.sh
# Deploy on Windows
.\build.ps1
```
