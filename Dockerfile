# Use Maven with OpenJDK 8 for building
FROM maven:3.8.6-openjdk-8-slim AS build

# Set the working directory
WORKDIR /app

# Copy the pom.xml and source code
COPY pom.xml .
COPY src ./src

# Build the application
RUN mvn clean package -DskipTests

# Use Eclipse Temurin 8 JRE for running
FROM eclipse-temurin:8-jre

# Set the working directory
WORKDIR /app

# Copy the built JAR from the build stage
COPY --from=build /app/target/*-jar-with-dependencies.jar app.jar

# Copy the default config file
COPY src/main/resources/com/ibm/jesseg/prometheus/config.json config.json

# Expose the default port
EXPOSE 9853

# Run the application
CMD ["java", "-jar", "app.jar"]