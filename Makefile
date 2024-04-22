PROJECT_NAME=ipk-project-2
APP_NAME=IPK.Project2.App
OUTPUTPATH = .

.PHONY: build publish clean

all: publish

build_app:
    @echo "Building app $(APP_NAME)"
    dotnet build $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj

publish: build_app
    @echo "Publishing $(APP_NAME)"
    dotnet publish $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj -p:PublishSingleFile=true -c Release -r linux-x64 --self-contained false  -o $(OUTPUTPATH)
    mv IPK.Project2.App ipk24chat-server
    @echo "Publishing $(APP_NAME) done."

clean:
    @echo "Cleaning $(PROJECT_NAME)"
    dotnet clean $(PROJECT_NAME)/$(APP_NAME)/$(APP_NAME).csproj
    rm -rf $(PROJECT_NAME)/$(APP_NAME)/bin $(PROJECT_NAME)/$(APP_NAME)/obj